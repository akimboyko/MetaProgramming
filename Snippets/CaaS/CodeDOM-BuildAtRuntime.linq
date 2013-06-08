<Query Kind="Program">
  <Namespace>System.CodeDom</Namespace>
  <Namespace>System.CodeDom.Compiler</Namespace>
  <Namespace>Microsoft.CSharp</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    var sw = Stopwatch.StartNew();

    // set target language
    const string programmingLanguage = "C#"; // or "VB" for Visual Basic.net

    // generate source code
    var sourceCode = GenerateSourceCode(BuildCodeNamespace(), programmingLanguage);
    
    sourceCode.Dump();
    
    // compile to assembly
    var generatedAssembly = CompileAssembly(sourceCode, programmingLanguage);
    
    Assembly.Load(generatedAssembly.GetName());
    
    // get first type
    var generatedType = generatedAssembly.ExportedTypes.Single();
    
    var processingMethodInfo = generatedType.GetMethods()
                    .Single(methodInfo => methodInfo.Name == "Test");
    
    Func<Model.ProcessingModel, Model.ReportModel> processingFunc = 
        model => (Model.ReportModel)processingMethodInfo.Invoke(null, new object[]{ model });
        
    IEnumerable<Model.ProcessingModel> models = 
                        Enumerable.Range(0, 1000000)
                            .Select(n => new Model.ProcessingModel { InputA = n, InputB = n * 0.5M, Factor = 0.050M });
    
    IEnumerable<Model.ReportModel> results =
                    models
                        .Select(processingFunc)
                        .ToList();
    
    sw.Stop();

    string.Format("Time taken: {0}ms", sw.Elapsed.TotalMilliseconds).Dump();
    
    results
        .Zip(models, (result, model) => new { result, model })
        .Select(@group => 
                    new
                    {
                        @return = @group.result,
                        ResultModel = @group.model
                    })
        .Take(10)
        .Dump();
}

// CodeDOM: Code-as-Data
static CodeNamespace BuildCodeNamespace()
{
    var ns = new CodeNamespace("Generated");
    
    var systemImport = new CodeNamespaceImport("System");
    ns.Imports.Add(systemImport);
    
    var programClass = new CodeTypeDeclaration("GeneartedClass");
    ns.Types.Add(programClass);
    
    var methodTest = new CodeMemberMethod
    {
        Attributes = MemberAttributes.Public | MemberAttributes.Static,
        Name = "Test",
        ReturnType = new CodeTypeReference(typeof(Model.ReportModel))
    };
    
    methodTest.Parameters.Add(new CodeParameterDeclarationExpression(
                                new CodeTypeReference(typeof(Model.ProcessingModel)),
                                "model"));
    
    var modelArgument = new CodeArgumentReferenceExpression("model");
    
    // model.Result = model.InputA + model.InputB * model.Factor;
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(modelArgument, "Result"),
            new CodeBinaryOperatorExpression(
                new CodePropertyReferenceExpression(modelArgument, "InputA"),
                CodeBinaryOperatorType.Add,
                new CodeBinaryOperatorExpression(
                    new CodePropertyReferenceExpression(modelArgument, "InputB"),
                    CodeBinaryOperatorType.Multiply,
                    new CodePropertyReferenceExpression(modelArgument, "Factor")))));
    
    // model.Delta = Math.Abs((model.Result ?? 0M) - model.InputA);
    // TODO: fix coalescing operator ??
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(modelArgument, "Delta"),
            new CodeBinaryOperatorExpression(
                new CodeMethodInvokeExpression(
                    new CodeMethodReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(Math)),
                        "Abs"),
                        
                        new CodeMethodInvokeExpression(
                            new CodeMethodReferenceExpression(
                                new CodePropertyReferenceExpression(modelArgument, "Result"),
                                "GetValueOrDefault"),
                                new [] { new CodePrimitiveExpression(0m) })),    
                        
                CodeBinaryOperatorType.Subtract,
                new CodePropertyReferenceExpression(modelArgument, "InputA"))));
    
    // model.Description = @"Some description";
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(modelArgument, "Description"),
            new CodePrimitiveExpression(@"Some description")));
    
    // return new Model.ReportModel { Σ = model.Result, Δ = model.Delta, λ = model.Description };
    methodTest.Statements.Add(
        new CodeVariableDeclarationStatement(
            typeof(Model.ReportModel),
            "reportModel",
            new CodeObjectCreateExpression(typeof(Model.ReportModel))));
    
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(
                new CodeVariableReferenceExpression("reportModel"), "Σ"),
            new CodePropertyReferenceExpression(modelArgument, "Result")));
    
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(
                new CodeVariableReferenceExpression("reportModel"), "Δ"),
            new CodePropertyReferenceExpression(modelArgument, "Delta")));
    
    methodTest.Statements.Add(
        new CodeAssignStatement(
            new CodePropertyReferenceExpression(
                new CodeVariableReferenceExpression("reportModel"), "λ"),
            new CodePropertyReferenceExpression(modelArgument, "Description")));
    
    methodTest.Statements.Add(
        new CodeMethodReturnStatement(
            new CodeVariableReferenceExpression("reportModel")));
    
    programClass.Members.Add(methodTest);
    
    return ns;
}

static string GenerateSourceCode(CodeNamespace prgNamespace, string programmingLanguage)
{
    var compilerOptions = new CodeGeneratorOptions()
    {
      IndentString = new string(' ', 4),
      BracingStyle = "C",
      BlankLinesBetweenMembers = false
    };
    var codeText = new StringBuilder();
    
    using (var codeWriter = new StringWriter(codeText))
    {
      CodeDomProvider.CreateProvider(programmingLanguage)
        .GenerateCodeFromNamespace(
          prgNamespace, codeWriter, compilerOptions);
    }
    
    return codeText.ToString();
}

static Assembly CompileAssembly(string sourceCode, string programmingLanguage)
{
    // dependency: "csc.exe"  
    var providerOptions = new Dictionary<string, string> { { "CompilerVersion", "v4.0" } };

    var codeProvider = CodeDomProvider.CreateProvider(programmingLanguage, providerOptions);
    
    var parameters = new CompilerParameters
    {
        GenerateInMemory = true
    };
    
    parameters.ReferencedAssemblies.Add(typeof(Model.ProcessingModel).Assembly.Location);
    
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
}

// model
namespace Model
{
    public class ProcessingModel
    {
        public decimal InputA { get; set; }
        public decimal InputB { get; set; }
        public decimal Factor { get; set; }
        
        public decimal? Result { get; set; }
        public decimal? Delta { get; set; }
        public string Description { get; set; }
        public decimal? Addition { get; set; }
    }
    
    public class ReportModel
    {
        public decimal? Σ { get; set; }
        public decimal? Δ { get; set; }
        public string λ { get; set; }
    }