<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Linq.Expressions.dll</Reference>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

void Main()
{
    // generate data
    IEnumerable<Model.ProcessingModel> models = 
                        Enumerable.Range(0, 1000000)
                            .Select(n => new Model.ProcessingModel { InputA = n, InputB = n * 0.5M, Factor = 0.050M });
    
    var sw = Stopwatch.StartNew();
    
    // INFO: λ-expression has mathematical origin, while Rolsyn CodeDOM/Roslyn trees represent abstraction over code    
    var modelParameter = Expression.Parameter(typeof(Model.ProcessingModel));
    
    var resultExpression = Expression.Parameter(typeof(Model.ReportModel));

    var expressionTree =
        Expression.Lambda<Func<Model.ProcessingModel, Model.ReportModel>>(
            Expression.Block(new Expression[]
                {
                    // model.Result = model.InputA + model.InputB * model.Factor;
                    Expression.Assign( 
                        Expression.Property(modelParameter, "Result"),
                        Expression.TypeAs(
                            Expression.Add(
                                Expression.Property(modelParameter, "InputA"),
                                Expression.Multiply(
                                    Expression.Property(modelParameter, "InputB"),
                                    Expression.Property(modelParameter, "Factor"))), typeof(decimal?))),
                    
                    // model.Delta = Math.Abs((model.Result ?? 0M) - model.InputA);
                    Expression.Assign( 
                        Expression.Property(modelParameter, "Delta"),
                        Expression.TypeAs(
                            Expression.Subtract(
                                Expression.Call(typeof(Math).GetMethod("Abs", new [] { typeof(decimal) }), 
                                    Expression.Coalesce(
                                        Expression.Property(modelParameter, "Result"),
                                        Expression.Constant(0m, typeof(decimal)))
                                        ),
                                Expression.Property(modelParameter, "InputA")), typeof(decimal?))),
                    
                    // model.Description = @"Some description";
                    Expression.Assign( 
                        Expression.Property(modelParameter, "Description"),
                        Expression.Constant(@"Some description", typeof(string))),
                    
                    // return new Model.ReportModel { Σ = model.Result, Δ = model.Delta, λ = model.Description };
                    Expression.MemberInit(
                        Expression.New(typeof(Model.ReportModel)),
                            Expression.Bind(
                                typeof(Model.ReportModel).GetMember("Σ").Single(),
                                Expression.Property(modelParameter, "Result")),
                            Expression.Bind(
                                typeof(Model.ReportModel).GetMember("Δ").Single(),
                                Expression.Property(modelParameter, "Delta")),
                            Expression.Bind(
                                typeof(Model.ReportModel).GetMember("λ").Single(),
                                Expression.Property(modelParameter, "Description")))
                    
                }), modelParameter);
    
    var compiledFunctionOutOfExpressionTree = expressionTree.Compile();
    
    IEnumerable<Model.ReportModel> results =
                    models
                        .Select(compiledFunctionOutOfExpressionTree)
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