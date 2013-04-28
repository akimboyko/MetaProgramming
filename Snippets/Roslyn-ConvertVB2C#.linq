<Query Kind="Statements">
  <Reference Relative="..\MetaProgramming\MetaProgramming\MetaProgramming.RoslynCTP\bin\Debug\MetaProgramming.RoslynCTP.dll">D:\work\Courses\MetaProgramming\MetaProgramming\MetaProgramming\MetaProgramming.RoslynCTP\bin\Debug\MetaProgramming.RoslynCTP.dll</Reference>
  <Namespace>MetaProgramming.RoslynCTP</Namespace>
  <IncludePredicateBuilder>true</IncludePredicateBuilder>
</Query>

// Code-as-Data
const string VbCode = @"
    Imports System

    Namespace Generated
        Public Class GeneartedClass
            Public Shared Function Test(ByVal model As Model.ProcessingModel) As Model.ReportModel
                model.Result = (model.InputA  _
                            + (model.InputB * model.Factor))
                model.Delta = (System.Math.Abs(model.Result.GetValueOrDefault(0D)) - model.InputA)
                model.Description = ""Some description""
                Dim reportModel As Model.ReportModel = New Model.ReportModel()
                reportModel.Σ = model.Result
                reportModel.Δ = model.Delta
                reportModel.λ = model.Description
                Return reportModel
            End Function
        End Class
    End Namespace";

ConvertTo.CSharp(VbCode).Dump("VB → C#: right way to convert code");