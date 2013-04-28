using ApprovalTests;
using ApprovalTests.Reporters;
using NUnit.Framework;

namespace MetaProgramming.RoslynCTP.Tests
{
    [TestFixture]
    [UseReporter(typeof(DiffReporter))]
    public class ConverterTests
    {
        private const string CSharpCode = @"
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

        private const string VbCode = @"
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

        [Test]
        public void CSharp2VB_Convert_ValidCode()
        {
            Approvals.Verify(ConvertTo.VisualBasic(CSharpCode));
        }

        [Test]
        public void VB2CSharp_Convert_ValidCode()
        {
            Approvals.Verify(ConvertTo.CSharp(VbCode));
        }
    }
}
