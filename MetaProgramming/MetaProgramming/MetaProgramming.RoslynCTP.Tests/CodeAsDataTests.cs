using System;
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
    public class CodeAsDataTests
    {
        [Test]
        public void ScriptAsData_ConvertJsonToScript_ScriptSubmission()
        {
            // Arrange
            var scriptInfo = JsonConvert.DeserializeObject<ScriptInfo>(File.ReadAllText(@"./ScriptInfo.json"));
            var dataClassesInfo = JsonConvert.DeserializeObject<ClassTemplateInfo[]>(File.ReadAllText(@"./ClassTemplateInfos.json"));
            Func<Type, object> deserializeToType = type => JsonConvert.DeserializeObject(File.ReadAllText(@"./InputData.json"), type.MakeArrayType());

            // Act
            IEnumerable<dynamic> results = CodeAsData.ProcessScript(scriptInfo, dataClassesInfo, deserializeToType);

            // Assert
            Approvals.Verify(JsonConvert.SerializeObject(results, Formatting.Indented));
        }
    }
}
