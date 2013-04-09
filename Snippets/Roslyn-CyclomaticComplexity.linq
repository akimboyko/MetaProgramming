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
    var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var workspace = Workspace.LoadSolution(solutionPath);
    var origianlSolution = workspace.CurrentSolution;
    
    cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
    
    var asyncSyntexRoots =
        origianlSolution
            .Projects
            .AsParallel()
                .AsUnordered()
            .SelectMany(project => project.Documents)
            .Select(document => document.GetSyntaxRootAsync(cancellationToken))
            .ToArray();
    
    var complexityBag = new ConcurrentBag<Complexity>();    
           
    Parallel.ForEach(
        asyncSyntexRoots,
        new ParallelOptions
        {
            CancellationToken = cancellationToken
        },
        syntaxRootAsync =>
            CalculateComplexity(syntaxRootAsync, complexityBag, cancellationToken));
    
    cancellationToken.ThrowIfCancellationRequested();
    
    complexityBag
        .GroupBy(complexity => complexity.TypeIdentifier)
        .OrderByDescending(@group => @group.Sum(complexity => complexity.nStatementSyntax))
        .Select(@group => @group.OrderByDescending(complexity => complexity.nStatementSyntax))
            .Dump();
}

const string solutionPath = 
    @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";
    //@"D:\temp\ninject-ninject-febf55a\Ninject.sln";

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