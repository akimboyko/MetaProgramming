using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Scripting.CSharp;

namespace MetaProgramming.RoslynCTP
{
    public class CodeAsData
    {
        public IEnumerable<dynamic> ProcessScript(ScriptInfo scriptInfo, IEnumerable<ClassTemplateInfo> dataClassesInfo, Func<Type, object> deserializeToType)
        {
            var modelType = LoadModelIntoAppDomain(dataClassesInfo);

            var scriptEngine = ConfigureScriptEngine(scriptInfo);

            IEnumerable<dynamic> models = deserializeToType(modelType) as IEnumerable<dynamic>;

            var submissionModel = Activator.CreateInstance(modelType);
            var session = scriptEngine.CreateSession(submissionModel);

            session.AddReference(modelType.Assembly);

            var submission = session.CompileSubmission<dynamic>(scriptInfo.Script);

            return models
                    .Select(model =>
                    {
                        //submissionModel.InputA = model.InputA;
                        //submissionModel.InputB = model.InputB;
                        //submissionModel.Factor = model.Factor;

                        return submission.Execute();
                    })
                    .ToList();
        }

        private static ScriptEngine ConfigureScriptEngine(ScriptInfo scriptInfo)
        {
            var scriptEngine = new ScriptEngine();

            scriptInfo.Assemblies.ToList().ForEach(scriptEngine.AddReference);
            scriptInfo.Namespaces.ToList().ForEach(scriptEngine.ImportNamespace);

            return scriptEngine;
        }

        private static Type LoadModelIntoAppDomain(IEnumerable<ClassTemplateInfo> dataClassesInfo)
        {
            var modelSourceCode = TranslateToModelSourceCode(dataClassesInfo);

            var syntaxTree = SyntaxTree.ParseText(modelSourceCode,
                                                    options:
                                                        new ParseOptions(languageVersion: LanguageVersion.CSharp5));

            if (syntaxTree.GetDiagnostics().Any())
            {
                throw new Exception(string.Format("Parsing failed: {0}",
                                        string.Join(", ", syntaxTree.GetDiagnostics().Select(diagnostic => diagnostic.Info.ToString()))));
            }

            var references = new[]
            {
                MetadataReference.CreateAssemblyReference(typeof(Console).Assembly.FullName),
                MetadataReference.CreateAssemblyReference(typeof(System.Threading.Tasks.Task).Assembly.FullName),
            };

            var modelDllName = string.Format("Model.{0}.dll", Guid.NewGuid());

            var compilation = Compilation.Create(
                                    outputName: modelDllName,
                                    options: new CompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                                    syntaxTrees: new[] { syntaxTree },
                                    references: references);

            if (compilation.GetDiagnostics().Any())
            {
                var exceptionMessage = new StringBuilder("Compilation failed: ")
                                            .Append(string.Join(", ",
                                                    compilation.GetDiagnostics()
                                                                .Select(diagnostic => diagnostic.Info.ToString())))
                                            .ToString();
                throw new Exception(exceptionMessage);
            }

            using (var stream = new FileStream(modelDllName, FileMode.OpenOrCreate))
            {
                var compileResult = compilation.Emit(stream);

                if (!compileResult.Success)
                {
                    throw new Exception(string.Format("Compilation failure: {0}", string.Join(", ", compileResult.Diagnostics)));
                }
            }

            var compiledAssembly = Assembly.LoadFile(Path.GetFullPath(modelDllName));

            return compiledAssembly.GetTypes().Single(type => type.Name == "ProcessingModel");
        }

        private static string TranslateToModelSourceCode(IEnumerable<ClassTemplateInfo> dataClassesInfo)
        {
            var runtimeTextTemplate = new RuntimeTextTemplate
                {
                    Session = new Dictionary<string, object>
                        {
                            { "namespaceName", "Model" },
                            { "classes", dataClassesInfo }
                        }
                };

            runtimeTextTemplate.Initialize();

            var modelSourceCode = runtimeTextTemplate.TransformText();
            return modelSourceCode;
        }
    }
}