using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MetaProgramming.RoslynCTP.Tests
{
    [TestFixture]
    public class CodeAsDataTests
    {
        [Test]
        public void ScriptAsData_ConvertJsonToScript_ScriptSubmission()
        {
            var scriptInfo = JsonConvert.DeserializeObject<ScriptInfo>(File.ReadAllText(@"./ScriptInfo.json"));
            var dataClassesInfo = JsonConvert.DeserializeObject<ClassTemplateInfo[]>(File.ReadAllText(@"./ClassTemplateInfos.json"));
            Func<Type, object> deserializeToType = type => JsonConvert.DeserializeObject(File.ReadAllText(@"./InputData.json"), type.MakeArrayType());

            new CodeAsData().ProcessScript(scriptInfo, dataClassesInfo, deserializeToType);

            Assert.That(true);
        }
    }
}
