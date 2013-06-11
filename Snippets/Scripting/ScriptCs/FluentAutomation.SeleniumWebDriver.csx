#r ".\packages\FluentAutomation.Core.2.0.0.2\lib\net40\FluentAutomation.Core.dll"
#r ".\packages\FluentAutomation.SeleniumWebDriver.2.0.0.2\lib\net40\FluentAutomation.SeleniumWebDriver.dll"

using System;
using System.IO;
using System.Reflection;
using FluentAutomation;
using FluentAutomation.Interfaces;

Settings.ScreenshotPath = @"d:\temp\";
Settings.ScreenshotOnFailedExpect = false;
Settings.ScreenshotOnFailedAction = false;
Settings.DefaultWaitTimeout = TimeSpan.FromSeconds(1);
Settings.DefaultWaitUntilTimeout = TimeSpan.FromSeconds(3);
Settings.MinimizeAllWindowsOnTestStart = false;

private static INativeActionSyntaxProvider I = null;

public static void Bootstrap<T>(string browserName)
{
	MethodInfo bootstrapMethod = null;
	ParameterInfo[] bootstrapParams = null;

	MethodInfo[] methods = typeof(T).GetMethods(BindingFlags.Static | BindingFlags.Public);
	foreach (var methodInfo in methods)
	{
		if (methodInfo.Name.Equals("Bootstrap"))
		{
			bootstrapMethod = methodInfo;
			bootstrapParams = methodInfo.GetParameters();
			if (bootstrapParams.Length == 1)
			{
				break;
			}
		}
	}

	var browserEnumValue = Enum.Parse(bootstrapParams[0].ParameterType, browserName);
	bootstrapMethod.Invoke(null, new object[] { browserEnumValue });

	I = new FluentTest().I;

    // hack to move drivers into bin so they can be located by Selenium (only prob on scriptcs atm)
    foreach (var driver in Directory.GetFiles(Environment.CurrentDirectory, "*.exe"))
	{
		var newFileName = Path.Combine(Environment.CurrentDirectory, "bin", Path.GetFileName(driver));
		if (!File.Exists(newFileName)) File.Move(driver, newFileName);
	}
}

Bootstrap<FluentAutomation.SeleniumWebDriver>("InternetExplorer");

try
{
	I.Open(@"http://localhost:8080/knockout.js/index.html");
       
    var firstInput = I.Find("input#firstName");
    I.Expect.Value("John").In(firstInput);
    
    var secondInput = I.Find("input#lastName");
    I.Expect.Value("Doe").In(secondInput);
    
    I.Enter("Ole").In(firstInput);

	I.Press("{TAB}");

    I.Enter("Hansenn").In(secondInput);

    I.Press("{TAB}");

    I.WaitUntil(() => I.Expect.Value("Ole Hansenn").In("span#fullName"));

    I.TakeScreenshot(@"test");
}
finally
{
	I.Dispose();

	if(File.Exists(@".\bin\IEDriverServer.exe"))
	{
	    File.Delete(@".\bin\IEDriverServer.exe");
	}
}

