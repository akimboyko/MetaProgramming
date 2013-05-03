<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <NuGetReference Prerelease="true">Microsoft.Bcl.Immutable</NuGetReference>
  <NuGetReference>Roslyn.Services.CSharp</NuGetReference>
  <NuGetReference>Rx-Core</NuGetReference>
  <NuGetReference>Rx-Linq</NuGetReference>
  <NuGetReference>Rx-Main</NuGetReference>
  <NuGetReference>Rx-PlatformServices</NuGetReference>
  <Namespace>Roslyn.Compilers.Common</Namespace>
  <Namespace>Roslyn.Compilers.CSharp</Namespace>
  <Namespace>Roslyn.Services</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <Namespace>System.Reactive.Joins</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
  <Namespace>System.Reactive.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    // prepare cancellationToken for async operations
    var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    // load workspace, i.e. solution from Visual Studio
    var workspace = Workspace.LoadSolution(solutionPath);
    var origianlSolution = workspace.CurrentSolution;
    
    // save a reference to original state
    cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
    
    // build syntax root asynchronously for all documents from all projects 
    var syntaxRootObservable =
            origianlSolution
                .Projects
                .AsParallel()
                    .AsUnordered()
                .SelectMany(project => project.Documents)
                .Select(document => document.GetSyntaxRootAsync(cancellationToken))
                    .ToObservable();
    
    var complexityBag = new ConcurrentBag<Complexity>();
    
    // calculate complexity for all methods using Rx Observables
    syntaxRootObservable
        .Subscribe(
            syntaxRootAsync =>
                CalculateComplexity(syntaxRootAsync, complexityBag, cancellationToken),
            cancellationToken);
    
    // throw an exception if more then 1 minute passed since start
    cancellationToken.ThrowIfCancellationRequested();
    
    // show results
    complexityBag
        .GroupBy(complexity => complexity.FilePath)
        .OrderByDescending(@group => @group.Sum(complexity => complexity.nStatementSyntax))
        .Select(@group => @group
							.OrderByDescending(complexity => complexity.nStatementSyntax))
            .Dump();
}

// cloc info about hibernate-core-master from github
//    5680 text files.
//    5515 unique files.
//     997 files ignored.
//
//http://cloc.sourceforge.net v 1.58  T=50.0 s (108.4 files/s, 15539.8 lines/s)
//-------------------------------------------------------------------------------
//Language                     files          blank        comment           code
//-------------------------------------------------------------------------------
//C#                            4007          61226          44433         341068
//MSBuild scripts                  6              0             42           5899
const string solutionPath = 
    @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

// statements for independent paths through a program's source code
private static readonly Func<StatementSyntax, bool> cyclomaticComplexityStatements =
        PredicateBuilder
            .False<StatementSyntax>()
            .Or(s => s is DoStatementSyntax)
            .Or(s => s is ForEachStatementSyntax)
            .Or(s => s is ForStatementSyntax)
            .Or(s => s is IfStatementSyntax)
            .Or(s => s is SwitchStatementSyntax)
            .Or(s => s is UsingStatementSyntax)
            .Or(s => s is WhileStatementSyntax)
                .Compile();

private class Complexity
{
    public string TypeIdentifier { get; set; }
    public string MethodIdentifier { get; set; }
	public string SourcesSample { get; set; }
    public int nStatementSyntax { get; set; }
	public string FilePath { get; set; }
	public int SourceLine { get; set; }
}

// process descendant nodes of syntaxRoot
private static async void CalculateComplexity(
                            Task<CommonSyntaxNode> syntaxRootAsync,
                            ConcurrentBag<Complexity> complexityBag,
                            CancellationToken cancellationToken)
{
    Array.ForEach(
        (await syntaxRootAsync)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(methodDeclaration =>
                        new Complexity
                        {
                            TypeIdentifier = ((TypeDeclarationSyntax)methodDeclaration.Parent).Identifier.ValueText,
                            MethodIdentifier = methodDeclaration.Identifier.ValueText,
                            SourcesSample = methodDeclaration.ToString(),
							nStatementSyntax = methodDeclaration.DescendantNodes()
                                                    .OfType<StatementSyntax>()
                                                    .Where(cyclomaticComplexityStatements)
                                                    .Count() + 1,
							FilePath = methodDeclaration.GetLocation().SourceTree.FilePath,
							SourceLine = methodDeclaration.GetLocation().SourceTree.GetLineSpan(methodDeclaration.Span, true, cancellationToken).StartLinePosition.Line
                        })
            .Where(complexity => complexity.nStatementSyntax > 10)
            .ToArray(),
        complexity => 
        {
            complexityBag.Add(complexity);
            cancellationToken.ThrowIfCancellationRequested();
        });
}