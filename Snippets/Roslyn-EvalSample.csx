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
        System.Console.ReadLine(), //fromValue:
        System.Console.ReadLine() //formula:
    };

var engine = new ScriptEngine();

Array.ForEach(
    new[] { typeof(object).Assembly },
    @assembly => engine.AddReference(@assembly));

Array.ForEach(
    new[] { "System" },
    @namespace => engine.ImportNamespace(@namespace));    

var session = engine.CreateSession();

object resultModel = null;

foreach(var script in scripts)
{
    resultModel = session
                    .CompileSubmission<object>(script)
                    .Execute();
}

Console.WriteLine(resultModel);