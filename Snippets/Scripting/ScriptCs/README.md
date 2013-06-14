ScriptCs for Project-less development
=====================================

Install ScriptCS
----------------

* Install [Chocolatey](http://chocolatey.org/)
* Install [ScriptCs](http://chocolatey.org/packages/ScriptCs): `cinst ScriptCs` 

REPL
----
1. `scriptcs -install`
2. `scriptcs`
3. `> using Newtonsoft.Json;`
4. `> JsonConvert.SerializeObject(DateTime.Now);`

ScriptCs and project-less environment and prototyping
-----------------------------------------------------

1. run shell with administrative rights, to be able to open port for listening
2. install WebApi script pack: `scriptcs -install ScriptCs.WebApi`
3. run script: `scriptcs.exe .\ScriptCs.WebApi.Sample.csx`

Scripting from C# using ScriptCs.Core
-------------------------------------

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
        scriptExecutor.Initialize(nuGetReferences, scriptPacks);
        scriptExecutor.Execute(scriptPath);
    }
    finally 
    {
        // restore current directory
        Environment.CurrentDirectory = previousCurrentDirectory;
    }

ScriptCs and Selenium/FluentAutomation
--------------------------------------

1. Add WebDriver to packages.config 
2. `scriptcs -install`
3. WebDriver + FireFox: `scriptcs.exe .\SeleniumWebDriver.FireFox.csx`
4. WebDriver + PhantomJs: `scriptcs.exe .\SeleniumWebDriver.PhantomJs.csx`

Edge.js: Node.js ↔ .Net == two platforms
----------------------------------------

1. Edge.js samples: `node EdgeJs2ClrSample.js`
2. Edge.js with *.csx file `node EdgeJs2ProjectlessCsx.js`

ScriptCs and PowerShell
-----------------------

* `import-module scriptcs`
* `invoke-scriptcs '"Hello PowerShell!"'`

see [ScriptCS-PowerShell-Module](https://github.com/beefarino/ScriptCS-PowerShell-Module) for details