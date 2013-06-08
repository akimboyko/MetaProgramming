<Query Kind="Statements">
  <Reference Relative="..\MetaProgramming\MetaProgramming\MetaProgramming.RoslynCTP\bin\Debug\MetaProgramming.RoslynCTP.dll">D:\work\Courses\MetaProgramming\MetaProgramming\MetaProgramming\MetaProgramming.RoslynCTP\bin\Debug\MetaProgramming.RoslynCTP.dll</Reference>
  <Namespace>MetaProgramming.RoslynCTP</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

const string CSharpCode = @"
            namespace Generated
            {
                using System;
    
                public class GeneartedClass
                {
                    public static Model.ReportModel Test(Model.ProcessingModel model)
                    {
                        model.Result = (model.InputA 
                                    + (model.InputB * model.Factor));
                        model.Delta = (System.Math.Abs(model.Result.GetValueOrDefault(0m)) - model.InputA);
                        model.Description = ""Some description"";
                        Model.ReportModel reportModel = new Model.ReportModel();
                        reportModel.Σ = model.Result;
                        reportModel.Δ = model.Delta;
                        reportModel.λ = model.Description;
                        return reportModel;
                    }
                }
            }";

ConvertTo.VisualBasic(CSharpCode).Dump("C# → VB: haters gonna hate!");