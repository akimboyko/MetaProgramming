// Steps:
//  * Get NuGet packages: `scriptcs -install`
//  * Run scriptcs: `scriptcs .\Roslyn-EvalSample.csx`

using System;
using System.Dynamic;
using Roslyn.Services.Interactive;
using Roslyn.Scripting.CSharp;

// limitations of scripts:
//  * no `dynamic` keyword allowed
//  * no async/await allowed
// Example:
//  var x = 6;
//  System.Math.Sqrt(x * 7)
var scripts = new []
    {
        System.Console.ReadLine(),  //fromValue:
        System.Console.ReadLine()   //formula:
    };

var engine = new ScriptEngine();    // create script engine

Array.ForEach(                      // add references to assembiles
    new[] { typeof(object).Assembly },
    @assembly => engine.AddReference(@assembly));

Array.ForEach(                      // import namespaces
    new[] { "System" },
    @namespace => engine.ImportNamespace(@namespace));    

var session = engine.CreateSession();// create session

object resultModel = null;

// INFO: scripts are using same session
foreach(var script in scripts)
{
    resultModel = CreateSession     // process scripts
                    .CompileSubmission<object>(script)
                    .Execute();
}

Console.WriteLine(resultModel);