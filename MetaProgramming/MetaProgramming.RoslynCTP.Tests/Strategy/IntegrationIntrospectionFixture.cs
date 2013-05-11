using System;

namespace MetaProgramming.RoslynCTP.Tests.Strategy
{
    public class IntegrationIntrospectionFixture : IIntrospectionFixture
    {
        // source code downloaded from https://github.com/nhibernate/nhibernate-core
        const string SolutionPath = @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

        public string GetTestType()
        {
            return "Integration";
        }

        public string GetSolutionPath()
        {
            return SolutionPath;
        }
    }
}
