using System;

namespace MetaProgramming.RoslynCTP.Tests.Strategy
{
    public interface IIntrospectionFixture
    {
        string GetTestType();
        string GetSolutionPath();

        TimeSpan SeachTimeOut { get; }
    }
}
