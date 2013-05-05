using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

namespace MetaProgramming.RoslynCTP
{
    public static class CodeAsData
    {
        public static IEnumerable<dynamic> ProcessScript(ScriptInfo scriptInfo, IEnumerable<ClassTemplateInfo> dataClassesInfo, Func<Type, object> deserializeToType)
        {
            var modelType = LoadModelTypesAppDomain(dataClassesInfo);

            // INFO: Configure Roslyn Script Engine
            var scriptEngine = ConfigureScriptEngine(scriptInfo, modelType.Assembly);

            // INFO: Read input data for Model of runtime types
            var models = LoadModelData(deserializeToType, modelType);

            Submission<object> submission;

            // INFO: Create Roslyn Script Submission
            var submissionModel = CreateSubmission(scriptInfo, modelType, scriptEngine, out submission);

            // INFO: Process all inputs
            return models
                    .Select(model =>
                        {
                            CopyPropertiesToSubmissionModel(modelType, submissionModel, model);

                            return submission.Execute();
                        })
                    .ToList();
        }

        private static ScriptEngine ConfigureScriptEngine(ScriptInfo scriptInfo, Assembly modelAssembly)
        {
            var scriptEngine = new ScriptEngine();

            scriptEngine.AddReference(modelAssembly);
            scriptInfo.Assemblies.ToList().ForEach(scriptEngine.AddReference);
            scriptInfo.Namespaces.ToList().ForEach(scriptEngine.ImportNamespace);

            return scriptEngine;
        }

        private static Type LoadModelTypesAppDomain(IEnumerable<ClassTemplateInfo> dataClassesInfo)
        {
            // INFO: Transform JSON configuration into C# code using runtime T4 template
            var modelSourceCode = TranslateToModelSourceCode(dataClassesInfo);

            // INFO: Build and load System.Type into AppDomain
            return BuildAndLoadModelTypesIntoAppDomain(modelSourceCode);
        }

        private static Type BuildAndLoadModelTypesIntoAppDomain(string modelSourceCode)
        {
            var parseOptions = new ParseOptions(
                compatibility: CompatibilityMode.None,
                languageVersion: LanguageVersion.CSharp5,
                preprocessorSymbols: new string[] {});

            var syntaxTree = SyntaxTree.ParseText(modelSourceCode, options: parseOptions);

            if (syntaxTree.GetDiagnostics().Any())
            {
                ThrowError("Parsing failed", syntaxTree.GetDiagnostics());
            }

            var references = new[]
                {
                    MetadataReference.CreateAssemblyReference(typeof (object).Assembly.FullName)
                };

            var modelDllName = string.Format("Model.{0}.dll", Guid.NewGuid());

            var compilation = Compilation.Create(
                outputName: modelDllName,
                options: new CompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                syntaxTrees: new[] {syntaxTree},
                references: references);

            if (compilation.GetDiagnostics().Any())
            {
                ThrowError("Compilation failed", compilation.GetDiagnostics());
            }

            using (var stream = new FileStream(modelDllName, FileMode.OpenOrCreate))
            {
                var compileResult = compilation.Emit(stream);

                if (!compileResult.Success)
                {
                    ThrowError("Compilation emit failed", compileResult.Diagnostics);
                }
            }

            var compiledAssembly = Assembly.LoadFrom(Path.GetFullPath(modelDllName));

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

            return runtimeTextTemplate.TransformText();
        }

        private static IEnumerable<object> LoadModelData(Func<Type, object> deserializeToType, Type modelType)
        {
            return deserializeToType(modelType) as IEnumerable<object>;
        }

        private static object CreateSubmission(ScriptInfo scriptInfo, Type modelType, ScriptEngine scriptEngine,
                                               out Submission<object> submission)
        {
            var submissionModel = Activator.CreateInstance(modelType);
            var session = scriptEngine.CreateSession(submissionModel, modelType);

            // INFO: Compile Rolsyn Script Submission
            submission = session.CompileSubmission<object>(scriptInfo.Script);

            return submissionModel;
        }

        private static void ThrowError(string message, IEnumerable<Diagnostic> diagnostics)
        {
            var exceptionMessage = new StringBuilder()
                .AppendFormat("{0}: ", message)
                .Append(string.Join(", ",
                                    diagnostics
                                        .Select(diagnostic => diagnostic.Info.ToString())))
                .ToString();

            throw new Exception(exceptionMessage);
        }

        private static void CopyPropertiesToSubmissionModel(Type modelType, object submissionModel, object model)
        {
            foreach (var property in modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                property.SetValue(submissionModel, property.GetValue(model));
            }
        }
    }
}