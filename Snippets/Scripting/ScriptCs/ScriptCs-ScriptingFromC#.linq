<Query Kind="Program">
  <NuGetReference>Autofac</NuGetReference>
  <NuGetReference>Common.Logging</NuGetReference>
  <NuGetReference>Nuget.Core</NuGetReference>
  <NuGetReference>ScriptCs.Core</NuGetReference>
  <NuGetReference>ScriptCs.Engine.Roslyn</NuGetReference>
  <Namespace>Common.Logging</Namespace>
  <Namespace>Common.Logging.Simple</Namespace>
  <Namespace>NuGet</Namespace>
  <Namespace>ScriptCs</Namespace>
  <Namespace>ScriptCs.Contracts</Namespace>
  <Namespace>ScriptCs.Engine.Roslyn</Namespace>
  <Namespace>ScriptCs.Package</Namespace>
  <Namespace>ScriptCs.Package.InstallationProvider</Namespace>
  <Namespace>Autofac</Namespace>
  <Namespace>Autofac.Core</Namespace>
</Query>

void Main()
{
    const string scriptPath = @"D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\sample.csx";
    
    // AutoFac container
    var builder = new ContainerBuilder();
    
    // register ScripotCs specific module
    builder.RegisterModule(new ScriptModule());
    
    using(var container = builder.Build())
    {
        using (var scope = container.BeginLifetimeScope())
        {
            var logger = scope.Resolve<ILog>();
            var executeScriptCs = scope.Resolve<ExecuteScriptCs>();
        
            try
            {
                executeScriptCs.Run(scriptPath);
            }
            catch(Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }
    }
}

public class ExecuteScriptCs
{
    // dependencies
    private readonly ILog logger;
    private readonly ScriptCs.IFileSystem fileSystem;
    private readonly IPackageAssemblyResolver packageAssemblyResolver;
    private readonly IPackageInstaller packageInstaller;
    private readonly IScriptPackResolver scriptPackResolver;
    private readonly IScriptExecutor scriptExecutor;
     
    public ExecuteScriptCs(ILog logger, ScriptCs.IFileSystem fileSystem, 
                            IPackageAssemblyResolver packageAssemblyResolver, IPackageInstaller packageInstaller,
                            IScriptPackResolver scriptPackResolver, IScriptExecutor scriptExecutor)
    {
        this.logger = logger;
        this.fileSystem = fileSystem;
        this.packageAssemblyResolver = packageAssemblyResolver;
        this.packageInstaller = packageInstaller;
        this.scriptPackResolver = scriptPackResolver;
        this.scriptExecutor = scriptExecutor;
    }
    
    // run script from file
    public void Run(string scriptPath)
    {
        // preserve current directory
        var previousCurrentDirectory = Environment.CurrentDirectory;
        
        try
        {
            // set directory to where script is
            // required to find NuGet dependencies
            Environment.CurrentDirectory = Path.GetDirectoryName(scriptPath);
        
            // prepare NuGet dependencies, download them if required
            var nuGetReferences = PreparePackages(
                                            scriptPath,
                                            fileSystem, packageAssemblyResolver,
                                            packageInstaller, logger.Info);
            
            // get script packs: not fully tested yet        
            var scriptPacks = scriptPackResolver.GetPacks();
            
            // execute script from file
            scriptExecutor.Execute(scriptPath, nuGetReferences, scriptPacks);
        }
        finally 
        {
            // restore current directory
            Environment.CurrentDirectory = previousCurrentDirectory;
        }
    }
    
    // prepare NuGet dependencies, download them if required
    private static IEnumerable<string> PreparePackages(
                            string scriptPath,
                            ScriptCs.IFileSystem fileSystem, IPackageAssemblyResolver packageAssemblyResolver,
                            IPackageInstaller packageInstaller, Action<string> outputCallback = null)
    {
        var workingDirectory = Path.GetDirectoryName(scriptPath);
        var binDirectory = Path.Combine(workingDirectory, ScriptCs.Constants.BinFolder);
    
        var packages = packageAssemblyResolver.GetPackages(workingDirectory);
        
        packageInstaller.InstallPackages(
                            packages,
                            allowPreRelease: true, packageInstalled: outputCallback);
    
        // current implementeation of RoslynCTP required dependencies to be in 'bin' folder
        if (!fileSystem.DirectoryExists(binDirectory))
        {
            fileSystem.CreateDirectory(binDirectory);
        }
    
        // copy dependencies one by one from 'packages' to 'bin'
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
}

// AutoFac configuration
public class ScriptModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .RegisterType<ScriptCs.FileSystem>()
            .As<ScriptCs.IFileSystem>()
            .SingleInstance();
        
        builder
            .RegisterType<ConsoleOutLogger>()
            .As<ILog>()
            .SingleInstance()
            .WithParameter("logName", @"Custom ScriptCs from C#") 
            .WithParameter("logLevel", Common.Logging.LogLevel.All)
            .WithParameter("showLevel", true)
            .WithParameter("showDateTime", true) 
            .WithParameter("showLogName", true) 
            .WithParameter("dateTimeFormat", @"yyyy-mm-dd hh:mm:ss"); 
         
        builder
            .RegisterType<FilePreProcessor>()
            .As<IFilePreProcessor>()
            .SingleInstance();
        
        builder
            .RegisterType<ScriptHostFactory>()
            .As<IScriptHostFactory>()
            .SingleInstance();
        
        builder
            .RegisterType<RoslynScriptEngine>()
            .As<IScriptEngine>();
            
        builder
            .RegisterType<ScriptExecutor>()
            .As<IScriptExecutor>();
        
        builder
            .RegisterType<NugetInstallationProvider>()
            .As<IInstallationProvider>()
            .SingleInstance();
        
        builder
            .RegisterType<PackageAssemblyResolver>()
            .As<IPackageAssemblyResolver>()
            .SingleInstance();
        
        builder
            .RegisterType<PackageContainer>()
            .As<IPackageContainer>()
            .SingleInstance();
        
        builder
            .RegisterType<PackageInstaller>()
            .As<IPackageInstaller>()
            .SingleInstance();
        
        builder
            .RegisterType<PackageManager>()
            .As<IPackageManager>()
            .SingleInstance();
        
        builder
            .RegisterType<ScriptPackResolver>()
            .As<IScriptPackResolver>()
            .SingleInstance();
            
        builder
            .RegisterType<ExecuteScriptCs>();
    }
}