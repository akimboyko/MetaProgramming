// Steps:
//  * Get NuGet packages: `scriptcs -install`
//  * Run scriptcs: `scriptcs .\Roslyn-EvalSample.csx`

using System;
using Roslyn.Services.Interactive;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

// limitations of scripts:
// * no `dynamic` keyword allowed
// * no async/await allowed

System.Console.WriteLine("Enter two commands to execute");
System.Console.WriteLine("For example:");
System.Console.WriteLine("var x = 6;");
System.Console.WriteLine("System.Math.Sqrt(x * 7)");
System.Console.WriteLine();

var scripts = new []
    {
        System.Console.ReadLine(),         //fromValue:
        System.Console.ReadLine()          //formula:
    };

var engine = new ScriptEngine();           // create script engine

Array.ForEach(                             // add references to assembiles
    new[] { typeof(object).Assembly, GetType().Assembly },
    @assembly => engine.AddReference(@assembly));

Array.ForEach(                             // import namespaces
    new[] { "System" },
    @namespace => engine.ImportNamespace(@namespace));    

var session = engine.CreateSession();      // create session

object resultModel = null;

// INFO: scripts are using same session
foreach(var script in scripts)
{
    resultModel = session                 // process scripts
                    .CompileSubmission<object>(script)
                    .Execute();
}

Console.WriteLine(resultModel);