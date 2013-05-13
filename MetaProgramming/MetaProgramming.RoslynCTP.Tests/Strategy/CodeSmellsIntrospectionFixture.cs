using System;
using System.IO;
using System.Linq;

namespace MetaProgramming.RoslynCTP.Tests.Strategy
{
    public class CodeSmellsIntrospectionFixture : IIntrospectionFixture
    {
        private const string SolutionFileName = @"CodeSmells.Samples.sln";
        private readonly string _solutionFullPath;

        public CodeSmellsIntrospectionFixture()
        {
            _solutionFullPath = Directory
                                    .GetFiles(Directory.GetCurrentDirectory(), SolutionFileName,
                                                SearchOption.AllDirectories)
                                    .SingleOrDefault();

            if (string.IsNullOrWhiteSpace(_solutionFullPath))
            {
                throw new FileNotFoundException(string.Format("Can't find solution '{0}' starting for '{1}'",
                                                                SolutionFileName, Directory.GetCurrentDirectory()),
                                                                SolutionFileName);
            }
        }

        public string GetTestType()
        {
            return "UnitTests";
        }

        public string GetSolutionPath()
        {
            return _solutionFullPath;
        }

        public TimeSpan SeachTimeOut
        {
            get { return TimeSpan.FromSeconds(20); }
        }
    }
}
