<Query Kind="Program">
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
  <Namespace>System.IO</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reflection</Namespace>
  <Namespace>System.Text</Namespace>
  <Namespace>System.CodeDom.Compiler</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    const string sourceCode = @"namespace DemoNamespace
        {
            using System;
            using System.Collections;
            using System.Threading;
            using System.Threading.Tasks;
        
            public static class Printer
            {
                public static async void Answer() 
                {
                    dynamic answer = 42;
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                    var output = await Task.Run(() => string.Format(""Universal [async] answer is '{0}'"", answer));
                    System.Console.WriteLine(output);
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                }
            }
         }";
    
    var sw = Stopwatch.StartNew();

    var generatedAssembly = CompileAssembly(sourceCode);
    
    Assembly.Load(generatedAssembly.GetName());
    
    var generatedType = generatedAssembly.ExportedTypes.Single();
    
    generatedType.GetMethods()
                    .Single(methodInfo => methodInfo.Name == "Answer")
                    .Invoke(null, null);
    
    sw.Stop();

    string.Format("Time taken: {0}ms", sw.Elapsed.TotalMilliseconds).Dump();
}

static Assembly CompileAssembly(string sourceCode)
{
    var codeProvider = CodeDomProvider.CreateProvider("CSharp");
    
    var parameters = new CompilerParameters
    {
        GenerateInMemory = true
    };
    
    parameters.ReferencedAssemblies.AddRange(
        new[]
        {
            @"System.Core.dll",
            @"Microsoft.CSharp.dll"
        });
    
    var results = codeProvider.CompileAssemblyFromSource(parameters, sourceCode);
    
    if(results.Errors.HasErrors)
    {  
        var errors = new StringBuilder("Following compilations error(s) found: ");
        
        errors.AppendLine();
        
        foreach (CompilerError error in results.Errors)
        {
            errors.AppendFormat("Message: '{0}', LineNumber: {1}", error.ErrorText, error.Line);
        }
        
        throw new Exception(errors.ToString());
    }
    
    return results.CompiledAssembly;
}