<Query Kind="Statements">
  <NuGetReference>Roslyn</NuGetReference>
  <NuGetReference>Roslyn.Compilers</NuGetReference>
  <NuGetReference>Roslyn.Compilers.Common</NuGetReference>
  <NuGetReference>Roslyn.Compilers.CSharp</NuGetReference>
  <NuGetReference>Roslyn.Services.Common</NuGetReference>
  <NuGetReference>Roslyn.Services.CSharp</NuGetReference>
  <Namespace>Roslyn.Services.Interactive</Namespace>
  <Namespace>Roslyn.Scripting.CSharp</Namespace>
  <Namespace>System.Dynamic</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

// limitations of scripts:
// * no `dynamic` keyword allowed
// * no async/await allowed
// For example:
// var x = 6;
// System.Math.Sqrt(x * 7)
var scripts = new []
    {
        Util.ReadLine(@"fromValue:"),       //fromValue:
        Util.ReadLine(@"formula:")          //formula:
    };

var engine = new ScriptEngine();           // create script engine

Array.ForEach(                             // add references to assembiles
    new[] { typeof(object).Assembly, GetType().Assembly },
    @assembly => engine.AddReference(@assembly));

Array.ForEach(                             // import namespaces
    new[] { "System" },
    @namespace => engine.ImportNamespace(@namespace));    

var session = engine.CreateSession();      // create session

dynamic resultModel = null;

// INFO: scripts are using same session
foreach(var script in scripts)
{
    resultModel = session                  // process scripts
                    .CompileSubmission<dynamic>(script)
                    .Execute();
}

(resultModel as object).Dump();