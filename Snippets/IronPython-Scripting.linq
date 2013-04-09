<Query Kind="Program">
  <NuGetReference>IronPython</NuGetReference>
  <Namespace>IronPython.Hosting</Namespace>
  <Namespace>Microsoft.Scripting</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

async void Main()
{   
    GetType().Assembly.FullName.Dump();

    IEnumerable<Model.ProcessingModel> models = 
                        Enumerable.Range(0, 1000000)
                            .Select(n => new Model.ProcessingModel { InputA = n, InputB = n * 0.5M, Factor = 0.050M });
    
    var sw = Stopwatch.StartNew();
    
    var pyCodeReadingTask = Task.FromResult(
                                File.ReadAllText(@"D:\work\Courses\CaaS\Snippets\sample.py"));

    var pyEngine = Python.CreateEngine();         
    var pyScope = pyEngine.CreateScope();
    var source = pyEngine.CreateScriptSourceFromString(await pyCodeReadingTask, SourceCodeKind.File);
    source.Execute(pyScope);
    
    dynamic businessRule = pyEngine.Operations.Invoke(pyScope.GetVariable("BusinessRule"));
    
    IEnumerable<dynamic> results =
                    models
                        .Select(model => businessRule.calculate(model))
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