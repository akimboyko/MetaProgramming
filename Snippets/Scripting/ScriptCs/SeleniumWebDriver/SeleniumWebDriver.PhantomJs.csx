#r "System.Drawing"
#r "D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\SeleniumWebDriver\bin\WebDriver.dll"
#r "D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\SeleniumWebDriver\bin\WebDriver.Support.dll"
#r "D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\SeleniumWebDriver\bin\nunit.framework.dll"
#r "D:\work\Courses\MetaProgramming\Snippets\Scripting\ScriptCs\SeleniumWebDriver\bin\FluentAssertions.dll"

using System;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.UI;
using FluentAssertions;

// hack to move drivers into bin so they can be located by Selenium (only prob on scriptcs atm)
foreach (var driver in Directory.GetFiles(Environment.CurrentDirectory, "*.exe", SearchOption.AllDirectories))
{
	var newFileName = Path.Combine(Environment.CurrentDirectory, "bin", Path.GetFileName(driver));
	File.Copy(driver, newFileName);
}

const string phantomjsExeFile = @".\bin\phantomjs.exe";

// create WebDriver instance
using (var driver = new PhantomJSDriver())
{
	try
	{
		driver.Navigate().GoToUrl(@"http://localhost:8080/knockout.js/index.html");

		var firstName = driver.FindElement(By.Id("firstName"));

		firstName.Clear();
		firstName.SendKeys("Ole");
		firstName.SendKeys(Keys.Tab);

		var lastName = driver.FindElement(By.Id("lastName"));

		lastName.Clear();
		lastName.SendKeys("Hansenn");
		lastName.SendKeys(Keys.Tab);

		WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(3));
		wait.Until(c => c.FindElement(By.Id("fullName")).Text.Contains("Ole Hansenn"));

		driver.FindElement(By.Id("fullName")).Text.Should().Be("Ole Hansenn");
	}
	finally
	{
		// create screenshot
		driver
			.GetScreenshot()
			.SaveAsFile("./test_phantomjs.png", System.Drawing.Imaging.ImageFormat.Png);

		driver.Quit();

		// cleanup phantomjs.exe binary
		if (File.Exists(phantomjsExeFile)) File.Delete(phantomjsExeFile);
	}
}