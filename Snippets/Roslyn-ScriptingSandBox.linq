<Query Kind="Statements">
  <NuGetReference>Roslyn</NuGetReference>
  <NuGetReference>Roslyn.Compilers</NuGetReference>
  <NuGetReference>Roslyn.Compilers.Common</NuGetReference>
  <NuGetReference>Roslyn.Compilers.CSharp</NuGetReference>
  <NuGetReference>Roslyn.Services.Common</NuGetReference>
  <NuGetReference>Roslyn.Services.CSharp</NuGetReference>
  <Namespace>Microsoft.CSharp.RuntimeHelpers</Namespace>
  <Namespace>Microsoft.Runtime.Hosting</Namespace>
  <Namespace>Microsoft.Runtime.Hosting.Interop</Namespace>
  <Namespace>Roslyn.Compilers</Namespace>
  <Namespace>Roslyn.Compilers.Common</Namespace>
  <Namespace>Roslyn.Compilers.Compilation</Namespace>
  <Namespace>Roslyn.Compilers.CSharp</Namespace>
  <Namespace>Roslyn.Scripting</Namespace>
  <Namespace>Roslyn.Scripting.CSharp</Namespace>
  <Namespace>Roslyn.Services</Namespace>
  <Namespace>Roslyn.Services.Classification</Namespace>
  <Namespace>Roslyn.Services.CodeCleanup</Namespace>
  <Namespace>Roslyn.Services.CodeGeneration</Namespace>
  <Namespace>Roslyn.Services.Completion</Namespace>
  <Namespace>Roslyn.Services.CSharp.Classification</Namespace>
  <Namespace>Roslyn.Services.FindReferences</Namespace>
  <Namespace>Roslyn.Services.Formatting</Namespace>
  <Namespace>Roslyn.Services.Host</Namespace>
  <Namespace>Roslyn.Services.Interactive</Namespace>
  <Namespace>Roslyn.Services.MetadataAsSource</Namespace>
  <Namespace>Roslyn.Services.Organizing</Namespace>
  <Namespace>Roslyn.Services.Shared.Extensions</Namespace>
  <Namespace>Roslyn.Utilities</Namespace>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Dynamic</Namespace>
  <Namespace>System.IO</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reflection</Namespace>
  <Namespace>System.Text</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

var sw = Stopwatch.StartNew();

var engine = new ScriptEngine();

new[]
{
    typeof (Math).Assembly,
    this.GetType().Assembly
}.ToList().ForEach(assembly => engine.AddReference(assembly));

new[]
{
    "System", "System.Math"
} .ToList().ForEach(@namespace => engine.ImportNamespace(@namespace));    

var script =
    @"
        using System;
        using System.IO;
        using System.Web.Http;
        using System.Web.Http.SelfHost;
        
        var address = ""http://localhost:8080"";
        var conf = new HttpSelfHostConfiguration(new Uri(address));
        conf.Routes.MapHttpRoute(name: ""DefaultApi"",
        routeTemplate: ""api/{controller}/{id}"",
        defaults: new { id = RouteParameter.Optional }
        );
        
        var server = new HttpSelfHostServer(conf);
        server.OpenAsync().Wait();
        Console.WriteLine(""Listening..."");
        Console.ReadKey();
    ";

var session = engine.CreateSession();
session.Execute(script);

sw.Stop();