<Query Kind="Program">
  <NuGetReference>Common.Logging</NuGetReference>
  <NuGetReference>Ninject</NuGetReference>
  <NuGetReference>Nuget.Core</NuGetReference>
  <NuGetReference>ScriptCs.Core</NuGetReference>
  <NuGetReference>ScriptCs.Engine.Roslyn</NuGetReference>
  <Namespace>Common.Logging</Namespace>
  <Namespace>Common.Logging.Simple</Namespace>
  <Namespace>Ninject</Namespace>
  <Namespace>Ninject.Modules</Namespace>
  <Namespace>ScriptCs</Namespace>
  <Namespace>ScriptCs.Contracts</Namespace>
  <Namespace>ScriptCs.Engine.Roslyn</Namespace>
  <Namespace>ScriptCs.Package</Namespace>
  <Namespace>ScriptCs.Package.InstallationProvider</Namespace>
  <Namespace>NuGet</Namespace>
</Query>

void Main()
{
    Environment.CurrentDirectory = workingDirectory;
    
    using(var kernel = new StandardKernel(new ScriptModule()))
    {
        var logger = kernel.Get<ILog>();
        var fileSystem = kernel.Get<ScriptCs.IFileSystem>();
        var packageAssemblyResolver = kernel.Get<IPackageAssemblyResolver>();
        var packageInstaller = kernel.Get<IPackageInstaller>();
        var scriptPackResolver = kernel.Get<IScriptPackResolver>();
        var scriptExecutor = kernel.Get<IScriptExecutor>();
    
        try
        {
            var nuGetReferences = PreparePackages(
                                    fileSystem, packageAssemblyResolver,
                                    packageInstaller, logger.Info);
            
            var scriptPacks = scriptPackResolver.GetPacks();
            // var scriptPacks = Enumerable.Empty<IScriptPack>();
            
            scriptExecutor.Execute(scriptPath, nuGetReferences, scriptPacks);
        }
        catch(Exception ex)
        {
            logger.Error(ex);
            throw;
        }
    }
}

private static IEnumerable<string> PreparePackages(
                        ScriptCs.IFileSystem fileSystem, IPackageAssemblyResolver packageAssemblyResolver,
                        IPackageInstaller packageInstaller, Action<string> outputCallback = null)
{
    var packages = packageAssemblyResolver.GetPackages(workingDirectory);
    
    packageInstaller.InstallPackages(
                        packages,
                        allowPreRelease: true, packageInstalled: outputCallback);

    if (!fileSystem.DirectoryExists(binDirectory))
    {
        fileSystem.CreateDirectory(binDirectory);
    }

    foreach(var assemblyName 
                in packageAssemblyResolver.GetAssemblyNames(workingDirectory, outputCallback))
    {
        var assemblyFileName = Path.GetFileName(assemblyName);
        var destFile = Path.Combine(binDirectory, assemblyFileName);
    
        var sourceFileLastWriteTime = fileSystem.GetLastWriteTime(assemblyName);
        var destFileLastWriteTime = fileSystem.GetLastWriteTime(destFile);

        if (sourceFileLastWriteTime == destFileLastWriteTime)
        {
             outputCallback(string.Format("Skipped: '{0}' because it is already exists", assemblyName));
        }
        else
        {
            fileSystem.Copy(assemblyName, destFile, overwrite: true);
            
            if(outputCallback != null)
            {
                outputCallback(string.Format("Copy: '{0}' to '{1}'", assemblyName, destFile));
            }
        }        
        
        yield return destFile;
    }
}

const string scriptPath = @"D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\sample.csx";
static readonly string workingDirectory = Path.GetDirectoryName(scriptPath);
static readonly string binDirectory = Path.Combine(workingDirectory, ScriptCs.Constants.BinFolder);

public class ScriptModule : NinjectModule
{
    public override void Load()
    {
        Bind<ScriptCs.IFileSystem>()
            .To<ScriptCs.FileSystem>()
            .InSingletonScope();
            
        Bind<ILog>()
            .To<ConsoleOutLogger>()
            .InSingletonScope()
            .WithConstructorArgument("logName", @"Custom ScriptCs from C#")
            .WithConstructorArgument("logLevel", Common.Logging.LogLevel.All)
            .WithConstructorArgument("showLevel", true)
            .WithConstructorArgument("showDateTime", true)
            .WithConstructorArgument("showLogName", true)
            .WithConstructorArgument("dateTimeFormat", @"yyyy-mm-dd hh:mm:ss");
            
        Bind<IFilePreProcessor>()
            .To<FilePreProcessor>()
            .InSingletonScope();
        
        Bind<IScriptHostFactory>()
            .To<ScriptHostFactory>()
            .InSingletonScope();
            
        Bind<IScriptEngine>()
            .To<RoslynScriptEngine>();
            
        Bind<IScriptExecutor>()
            .To<ScriptExecutor>();
            
        Bind<IInstallationProvider>()
            .To<NugetInstallationProvider>()
            .InSingletonScope();
            
        Bind<IPackageAssemblyResolver>()
            .To<PackageAssemblyResolver>()
            .InSingletonScope();
            
        Bind<IPackageContainer>()
            .To<PackageContainer>()
            .InSingletonScope();
        
        Bind<IPackageInstaller>()
            .To<PackageInstaller>()
            .InSingletonScope();    
           
        Bind<IPackageManager>()
            .To<PackageManager>()
            .InSingletonScope();   
            
        Bind<IScriptPackResolver>()
            .To<ScriptPackResolver>()
            .InSingletonScope();
    }
}