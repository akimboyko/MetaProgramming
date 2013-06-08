<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <NuGetReference Prerelease="true">Microsoft.Bcl.Immutable</NuGetReference>
  <NuGetReference>Roslyn.Services.CSharp</NuGetReference>
  <Namespace>Roslyn.Compilers.Common</Namespace>
  <Namespace>Roslyn.Compilers.CSharp</Namespace>
  <Namespace>Roslyn.Services</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    // prepare cancellationToken for async operations
    var cancellationTokenSource = new CancellationTokenSource();
	cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
    var cancellationToken = cancellationTokenSource.Token;

	IEnumerable<ReturnNull> returnNulls;
	
	var stopwatch = new Stopwatch();
	stopwatch.Start();

    // load workspace, i.e. solution from Visual Studio
    using(var workspace = Workspace.LoadSolution(solutionPath))
	{
		// save a reference to original state
		var origianlSolution = workspace.CurrentSolution;
		
		// build syntax root and semantic model asynchronously in parallel 
		// for all documents from all projects 
		returnNulls =
			origianlSolution
				.Projects
				.AsParallel()
					.AsUnordered()
				.WithCancellation(cancellationToken)
				.SelectMany(project => project.Documents)
				.Select(document => document.GetSyntaxRootAsync(cancellationToken))
				// calculate complexity for all methods in parallel
				.SelectMany(syntaxRootAsync => 
							FindReturnNull(syntaxRootAsync, cancellationToken).Result)
				.ToArray();
		
		// throw an exception if more then 1 minute passed since start
		cancellationToken.ThrowIfCancellationRequested();
	}
    
	stopwatch.Stop();
	stopwatch.Elapsed.Dump("Elapsed time");
	
    // show results
    returnNulls
        .GroupBy(returnNull => returnNull.FilePath)
		.OrderBy(returnNullByFilePath => returnNullByFilePath.Key)
		.Select(returnNullByFilePath => new
					{
						FilePath = returnNullByFilePath.Key,
						ReturnNulls = 
							returnNullByFilePath
								.OrderBy(returnNull => returnNull.SourceLine)
								.Select(returnNull => new { returnNull.SourceLine, returnNull.SourcesSample })
					})
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

// statements for `return null;`
private static readonly Func<StatementSyntax, bool> returnNullStatement =
        PredicateBuilder
			.True<StatementSyntax>()
            .And(s => s is ReturnStatementSyntax)
			.And(s => (s as ReturnStatementSyntax).Expression != null)
			.And(s => (s as ReturnStatementSyntax).Expression.Kind == SyntaxKind.NullLiteralExpression)
                .Compile();

// process descendant nodes of syntaxRoot
private static async Task<IEnumerable<ReturnNull>> FindReturnNull(
                            Task<CommonSyntaxNode> syntaxRootAsync,
                            CancellationToken cancellationToken)
{
	var walker = new ReturnNullWalker(cancellationToken);
	
	walker.Visit((await syntaxRootAsync) as SyntaxNode);
	
	return walker.Results;
}

private class ReturnNullWalker : SyntaxWalker
{
	private readonly List<ReturnNull> results = new List<ReturnNull>();
	private readonly CancellationToken cancellationToken;

	public ReturnNullWalker(CancellationToken cancellationToken)
	{
		this.cancellationToken = cancellationToken;
	}

	public IEnumerable<ReturnNull> Results { get { return results; } }

	public override void VisitReturnStatement(ReturnStatementSyntax returnNull)
    {
    	base.VisitReturnStatement(returnNull);
		
		if(returnNullStatement(returnNull))
		{
			results.Add(
				new ReturnNull
				{
					TypeIdentifier = GetParentSyntax<TypeDeclarationSyntax>(returnNull).Identifier.ValueText,
						SourcesSample = returnNull.ToString(),
						FilePath = returnNull.GetLocation().SourceTree.FilePath,
						SourceLine = returnNull
										.GetLocation().SourceTree
										.GetLineSpan(returnNull.Span, true, cancellationToken)
											.StartLinePosition.Line + 1
				});
		}
    }
}

private class ReturnNull
{
    public string TypeIdentifier { get; set; }
	public string SourcesSample { get; set; }
    public string FilePath { get; set; }
    public int SourceLine { get; set; }
}

private static TDeclarationSyntax GetParentSyntax<TDeclarationSyntax>(SyntaxNode statementSyntax)
					where TDeclarationSyntax : MemberDeclarationSyntax
{
	SyntaxNode statement = statementSyntax;
	while(statement != null && !(statement is TDeclarationSyntax))
	{
		statement = statement.Parent;
	}
	
	if(statement == null || !(statement is TDeclarationSyntax))
	{
		throw new Exception(string.Format("Can't find parent {0} node", typeof(TDeclarationSyntax)));
	}
	
	return (TDeclarationSyntax)statement;
}