using System;
using System.Collections.Generic;
using ApprovalTests;
using ApprovalTests.Reporters;
using NUnit.Framework;

namespace MetaProgramming.RoslynCTP.Tests
{
    [TestFixture]
    [UseReporter(typeof(DiffReporter))]
    public class RuntimeTextTemplateTests
    {
        [Test]
        public void RuntimeTextTemplate_TransformText_ExpectedOutput()
        {
            var runtimeTextTemplate = new RuntimeTextTemplate
                {
                    Session = new Dictionary<string, object>
                        {
                            { "namespaceName", "Model" },
                            { "classes", new[]
                                {
                                    new ClassTemplateInfo
                                        {
                                            Name = "ProcessingModel",
                                            IsPublic = true,
                                            Properties = new Dictionary<string, Type>
                                                {
                                                    { "InputA", typeof(decimal) },
                                                    { "InputB", typeof(decimal) },
                                                    { "Factor", typeof(decimal) },
                                                    { "Result", typeof(decimal?) },
                                                    { "Delta",  typeof(decimal?) },
                                                    { "Description",  typeof(string) },
                                                    { "Addition",  typeof(decimal?) },
                                                }
                                        },
                                    new ClassTemplateInfo
                                        {
                                            Name = "ReportModel",
                                            IsPublic = true,
                                            Properties = new Dictionary<string, Type>
                                                {
                                                    { "Σ", typeof(decimal?) },
                                                    { "Δ", typeof(decimal?) },
                                                    { "λ",  typeof(string) },
                                                }
                                        }
                                }
                            }
                        }
                };

            runtimeTextTemplate.Initialize();

            Approvals.Verify(runtimeTextTemplate.TransformText());
        }
    }
}