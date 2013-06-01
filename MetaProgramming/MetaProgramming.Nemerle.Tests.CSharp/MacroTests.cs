using FluentAssertions;
using NUnit.Framework;

namespace MetaProgramming.Nemerle.Tests.CSharp
{
    [TestFixture]
    public class MacroTests
    {
        [Test]
        public void CompileTimeVsRunTime_Execution_ExpectedOutput()
        {
            string result = new CompileTimeVsRunTimeExecutionSample().Execute();

            result.Should().Be("output");
        }
    }
}
