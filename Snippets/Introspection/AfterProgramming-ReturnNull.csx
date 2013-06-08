// scriptcs -install -pre — run to install dependencies

using System.Threading;
using System.Diagnostics;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using Newtonsoft.Json;
using AfterProgramming;
using AfterProgramming.Internal;

// change path to solution
const string solutionPath = @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var cancellationToken = cancellationTokenSource.Token;

Console.WriteLine("Start introspection at {0}…", solutionPath);

var watch = Stopwatch.StartNew();

var results = new ReturnStatement()
	.SearchForReturnNullStatements(solutionPath, cancellationToken);

watch.Stop();

Console.WriteLine(
	JsonConvert.SerializeObject(
		results.Take(10), Formatting.Indented));

Console.WriteLine("Finish introspection in {0} ms", watch.Elapsed);