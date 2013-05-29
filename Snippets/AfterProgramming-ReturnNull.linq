<Query Kind="Statements">
  <NuGetReference Prerelease="true">AfterProgramming</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Roslyn.Compilers.CSharp</NuGetReference>
  <Namespace>AfterProgramming</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

const string solutionPath = @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var cancellationToken = cancellationTokenSource.Token;

Console.WriteLine("Start introspection…");

var watch = Stopwatch.StartNew();

var results = new ReturnStatement()
	.SearchForReturnNullStatements(solutionPath, cancellationToken);

watch.Stop();

JsonConvert.SerializeObject(results.Take(10), Newtonsoft.Json.Formatting.Indented).Dump();

Console.WriteLine("Finish introspection in {0} ms", watch.Elapsed);