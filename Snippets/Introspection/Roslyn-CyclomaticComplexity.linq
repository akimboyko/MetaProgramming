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
	cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
    var cancellationToken = cancellationTokenSource.Token;

	IEnumerable<Complexity> complexities;

	var stopwatch = new Stopwatch();
	stopwatch.Start();

    // load workspace, i.e. solution from Visual Studio
    using(var workspace = Workspace.LoadSolution(solutionPath))
    {
		// save a reference to original state
		var origianlSolution = workspace.CurrentSolution;
		
		// build syntax root asynchronously in parallel for all documents from all projects 
		complexities =
			origianlSolution
				.Projects
				.AsParallel()
					.AsUnordered()
				.WithCancellation(cancellationToken)
				.SelectMany(project => project.Documents)
				.Select(document => document.GetSyntaxRootAsync(cancellationToken))
				// calculate complexity for all methods in parallel
				.SelectMany(syntaxRootAsync =>
								CalculateComplexity(syntaxRootAsync, cancellationToken).Result)
				.ToArray();
		
		// throw an exception if more then 1 minute passed since start
		cancellationToken.ThrowIfCancellationRequested();
    }
	
	stopwatch.Stop();
	stopwatch.Elapsed.Dump("Elapsed time");
	
    // show results
    complexities
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
//MSBuild scripts                  6              0             42           5899
const string solutionPath = @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

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

// process descendant nodes of syntaxRoot
private static async Task<IEnumerable<Complexity>> CalculateComplexity(
                            Task<CommonSyntaxNode> syntaxRootAsync,
                            CancellationToken cancellationToken)
{
    return
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
            .ToArray();
}

private class Complexity
{
    public string TypeIdentifier { get; set; }
    public string MethodIdentifier { get; set; }
	public string SourcesSample { get; set; }
    public int nStatementSyntax { get; set; }
	public string FilePath { get; set; }
	public int SourceLine { get; set; }
}