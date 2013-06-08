<Query Kind="Program">
  <NuGetReference>Common.Logging</NuGetReference>
  <NuGetReference>Ninject</NuGetReference>
  <NuGetReference>ScriptCs.Core</NuGetReference>
  <NuGetReference>ScriptCs.Engine.Roslyn</NuGetReference>
  <Namespace>Common.Logging.Simple</Namespace>
  <Namespace>ScriptCs</Namespace>
  <Namespace>ScriptCs.Contracts</Namespace>
  <Namespace>ScriptCs.Engine.Roslyn</Namespace>
  <Namespace>Ninject.Modules</Namespace>
  <Namespace>Common.Logging</Namespace>
  <Namespace>Ninject</Namespace>
</Query>

void Main()
{
    using(var kernel = new StandardKernel(new ScriptModule()))
    {
        var scriptExecutor = kernel.Get<IScriptExecutor>();
        
        scriptExecutor.Execute(
                            @"D:\work\Courses\MetaProgramming\Snippets\Scripting\sample.csx",
                            Enumerable.Empty<string>(), Enumerable.Empty<IScriptPack>());
    }
}

public class ScriptModule : NinjectModule
{
    public override void Load()
    {
        Bind<IFileSystem>()
            .To<FileSystem>();
            
        Bind<ILog>()
            .To<ConsoleOutLogger>()
            .WithConstructorArgument("logName", @"Custom ScriptCs from C#")
            .WithConstructorArgument("logLevel", Common.Logging.LogLevel.All)
            .WithConstructorArgument("showLevel", true)
            .WithConstructorArgument("showDateTime", true)
            .WithConstructorArgument("showLogName", true)
            .WithConstructorArgument("dateTimeFormat", @"yyyy-mm-dd hh:mm:ss");
            
        Bind<IFilePreProcessor>()
            .To<FilePreProcessor>();
        
        Bind<IScriptHostFactory>()
            .To<ScriptHostFactory>();
            
        Bind<IScriptEngine>()
            .To<RoslynScriptEngine>();
            
        Bind<IScriptExecutor>()
            .To<ScriptExecutor>();
    }
}