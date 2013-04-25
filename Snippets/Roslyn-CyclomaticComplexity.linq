<Query Kind="Program">
  <NuGetReference>Roslyn.Services.CSharp</NuGetReference>
  <Namespace>Roslyn.Compilers.CSharp</Namespace>
  <Namespace>Roslyn.Services</Namespace>
  <Namespace>Roslyn.Compilers.Common</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    // prepare cancellationToken for async operations
    var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    // load workspace, i.e. solution from Visual Studio
    var workspace = Workspace.LoadSolution(solutionPath);
    
    // save a reference to original state
    var origianlSolution = workspace.CurrentSolution;
    
    cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
    
    // build syntax root asynchronously in parallel for all documents from all projects 
    var asyncSyntexRoots =
        origianlSolution
            .Projects
            .AsParallel()
                .AsUnordered()
            .SelectMany(project => project.Documents)
            .Select(document => document.GetSyntaxRootAsync(cancellationToken))
            .ToArray();
    
    var complexityBag = new ConcurrentBag<Complexity>();    

    // calculate complexity for all methods in parallel
    Parallel.ForEach(
        asyncSyntexRoots,
        new ParallelOptions
        {
            CancellationToken = cancellationToken
        },
        syntaxRootAsync =>
            CalculateComplexity(syntaxRootAsync, complexityBag, cancellationToken));
    
    // throw an exception if more then 1 minute passed since start
    cancellationToken.ThrowIfCancellationRequested();
    
    // show results
    complexityBag
        .GroupBy(complexity => complexity.TypeIdentifier)
        .OrderByDescending(@group => @group.Sum(complexity => complexity.nStatementSyntax))
        .Select(@group => @group.OrderByDescending(complexity => complexity.nStatementSyntax))
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
//XML                           1222          11306           1185         238449
//XSLT                           118           6950           3103          46141
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
    public int nStatementSyntax { get; set; }
}

// process descendant nodes asynchronously for syntaxRoot
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
                            nStatementSyntax = methodDeclaration.DescendantNodes()
                                                    .OfType<StatementSyntax>()
                                                    .Where(cyclomaticComplexityStatements)
                                                    .Count() + 1
                        })
            .Where(complexity => complexity.nStatementSyntax > 10)
            .ToArray(),
        complexity => 
        {
            complexityBag.Add(complexity);
            cancellationToken.ThrowIfCancellationRequested();
        });
}