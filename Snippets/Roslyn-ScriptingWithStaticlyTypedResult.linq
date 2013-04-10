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
  <Namespace>System.Dynamic</Namespace>
  <Namespace>System.IO</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Reflection</Namespace>
  <Namespace>System.Text</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
//    IEnumerable<Model.ProcessingModel> models = new[]
//    {
//        new Model.ProcessingModel { InputA = 10M, InputB = 5M, Factor = 0.050M },
//        new Model.ProcessingModel { InputA = 20M, InputB = 2M, Factor = 0.020M },
//        new Model.ProcessingModel { InputA = 12M, InputB = 3M, Factor = 0.075M },
//        new Model.ProcessingModel { InputA =  0M, InputB = 9M, Factor = 0.800M },
//    };

    IEnumerable<Model.ProcessingModel> models = 
                        Enumerable.Range(0, 1000000)
                            .Select(n => new Model.ProcessingModel { InputA = n, InputB = n * 0.5M, Factor = 0.050M });
    
    var sw = Stopwatch.StartNew();
    
    var engine = new ScriptEngine();
    
    new[]
    {
        typeof (Math).Assembly,
        this.GetType().Assembly
    }.ToList().ForEach(assembly => engine.AddReference(assembly));
    
    new[]
    {
        "System", "System.Math", 
        typeof(Model.ProcessingModel).Namespace
    } .ToList().ForEach(@namespace => engine.ImportNamespace(@namespace));    

    // limitations of script:
    // * no dynamic allowed
    // * no async/await allowed
    var script =
        @"
        Result = InputA + InputB * Factor;
        Delta = Math.Abs((Result ?? 0M) - InputA);
        Description = ""Some description"";
        new Model.ReportModel { Σ = Result, Δ = Delta, λ = Description }
        ";
    
    var submissionModel = new Model.ProcessingModel();
    var session = engine.CreateSession(submissionModel);
    var submission = session.CompileSubmission<Model.ReportModel>(script);
    
    IEnumerable<Model.ReportModel> results =
                    models
                        .Select(model =>
                                    {
                                        submissionModel.InputA = model.InputA;
                                        submissionModel.InputB = model.InputB;
                                        submissionModel.Factor = model.Factor;
                                        
                                        return submission.Execute();
                                    })
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
}

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