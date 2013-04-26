// Steps:
// * Get NuGet packages: `scriptcs -install`
// * Run scriptcs: `scriptcs .\Roslyn-EvalSample.csx`

using System;
using System.Dynamic;
using Roslyn.Services.Interactive;
using Roslyn.Scripting.CSharp;

// limitations of scripts:
// * no `dynamic` keyword allowed
// * no async/await allowed
// For example:
// var x = 6;
// System.Math.Sqrt(x * 7)
var scripts = new []
    {
        //fromValue:
        System.Console.ReadLine(),
        //formula:
        System.Console.ReadLine()
    };

// create script engine
var engine = new ScriptEngine();

// add references to assembiles
Array.ForEach(
    new[] { typeof(object).Assembly },
    @assembly => engine.AddReference(@assembly));

// import namespaces
Array.ForEach(
    new[] { "System" },
    @namespace => engine.ImportNamespace(@namespace));    

// create session
var session = engine.CreateSession();

object resultModel = null;

// process scripts
// INFO: scripts are using same session
foreach(var script in scripts)
{
    resultModel = session
                    .CompileSubmission<object>(script)
                    .Execute();
}

Console.WriteLine(resultModel);