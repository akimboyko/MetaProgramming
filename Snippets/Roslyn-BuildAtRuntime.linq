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
  <Namespace>System.IO</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reflection</Namespace>
  <Namespace>System.Text</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

const string codeSnippet = @"namespace DemoNamespace
    {
        using System;
        using System.Collections;
        using System.Threading;
        using System.Threading.Tasks;
    
        public class Printer
        {
            public void Answer() 
            {
                dynamic answer = 42; // not working with RoslynCTP Set'12
                //int answer = 42;
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                var output = await Task.Run(() => string.Format(""Universal [async] answer is '{0}'"", answer)); // not working with RoslynCTP Set'12
                //var output = Task.Run(() => string.Format(""Universal [async] answer is '{0}'"", answer)).Result;
                System.Console.WriteLine(output);
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            }
        }
     }";

var syntaxTree = SyntaxTree.ParseText(codeSnippet,
     options: new ParseOptions(languageVersion: LanguageVersion.CSharp5));

if(syntaxTree.GetDiagnostics().Any())
{
    syntaxTree.GetDiagnostics().Select(diagnostic => diagnostic.Info.ToString()).Dump();
    throw new Exception("Parsing failed");
}

var references = new []
{
	MetadataReference.CreateAssemblyReference(typeof(Console).Assembly.FullName),
	MetadataReference.CreateAssemblyReference(typeof(System.Threading.Tasks.Task).Assembly.FullName),
};

var compilation = Compilation.Create(
                        outputName: "Demo", 
                        options: new CompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                        syntaxTrees: new[] { syntaxTree },
                        references: references);

if(compilation.GetDiagnostics().Any())
{
    var exceptionMessage = new StringBuilder("Compilation failed: ")
                                .Append(string.Join(", ",
                                        compilation.GetDiagnostics()
                                                    .Select(diagnostic => diagnostic.Info.ToString())))
                                .ToString();
    throw new Exception(exceptionMessage);
}

Assembly compiledAssembly;
using (var stream = new MemoryStream())
{
    EmitResult compileResult = compilation.Emit(stream);
    compiledAssembly = Assembly.Load(stream.GetBuffer());
}

dynamic instance = Activator.CreateInstance(compiledAssembly.GetTypes().First());
instance.Answer();

//compilation.GetTypeByMetadataName("System.String").Interfaces.Select(@interface => @interface.Name).Dump();

SemanticModel semanticModel =  compilation.GetSemanticModel(syntaxTree);

var methodDeclaration = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
var expression = syntaxTree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>().First();

//semanticModel.GetDeclaredSymbol(methodDeclaration).Dump();
//semanticModel.GetTypeInfo(expression).Type.Dump();