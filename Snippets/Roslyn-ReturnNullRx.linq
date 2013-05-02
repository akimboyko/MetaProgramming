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
    
    var returnNullBag = new ConcurrentBag<ReturnNull>();
    
    // search for `return null;` for all methods using Rx Observables
    syntaxRootObservable
        .Subscribe(
            syntaxRootAsync =>
                FindReturnNull(syntaxRootAsync, returnNullBag, cancellationToken),
            cancellationToken);
    
    // throw an exception if more then 1 minute passed since start
    cancellationToken.ThrowIfCancellationRequested();
    
    // show results
    returnNullBag
        .GroupBy(returnNull => returnNull.FilePath)
			.Select(returnNulls => returnNulls.OrderBy(returnNull => returnNull.SourceLine))
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

private class ReturnNull
{
    public string TypeIdentifier { get; set; }
    public string FilePath { get; set; }
    public int SourceLine { get; set; }
}

// process descendant nodes of syntaxRoot
private static async void FindReturnNull(
                            Task<CommonSyntaxNode> syntaxRootAsync,
                            ConcurrentBag<ReturnNull> returnNullBag,
                            CancellationToken cancellationToken)
{
    Array.ForEach(
        (await syntaxRootAsync)
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
			.Where(returnNullStatement)
            .Select(returnNull =>
                        new ReturnNull
                        {
                            TypeIdentifier = GetParentSyntax<TypeDeclarationSyntax>(returnNull).Identifier.ValueText,
							FilePath = returnNull.GetLocation().SourceTree.FilePath,
							SourceLine = returnNull
											.GetLocation().SourceTree
											.GetLineSpan(returnNull.Span, true, cancellationToken)
												.StartLinePosition.Line + 1
                        })
            .ToArray(),
        returnNull => 
        {
            returnNullBag.Add(returnNull);
            cancellationToken.ThrowIfCancellationRequested();
        });
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