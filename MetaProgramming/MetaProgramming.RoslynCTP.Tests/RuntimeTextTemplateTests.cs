using System.Collections.Generic;
using System.IO;
using ApprovalTests;
using ApprovalTests.Reporters;
using Newtonsoft.Json;
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
            // Act
            var runtimeTextTemplate = new RuntimeTextTemplate
                {
                    Session = new Dictionary<string, object>
                        {
                            { "namespaceName", "Model" },
                            { "classes", JsonConvert.DeserializeObject<ClassTemplateInfo[]>(File.ReadAllText(@"ClassTemplateInfos.json")) }
                        }
                };

            runtimeTextTemplate.Initialize();

            // Assert
            Approvals.Verify(runtimeTextTemplate.TransformText());
        }
    }
}