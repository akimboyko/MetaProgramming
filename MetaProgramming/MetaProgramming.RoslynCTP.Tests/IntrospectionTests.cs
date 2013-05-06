using System;
using System.Collections.Generic;
using System.Threading;
using ApprovalTests;
using ApprovalTests.Reporters;
using MetaProgramming.RoslynCTP.Model;
using NUnit.Framework;
using Newtonsoft.Json;

namespace MetaProgramming.RoslynCTP.Tests
{
    [TestFixture]
    [UseReporter(typeof(DiffReporter))]
    public class IntrospectionTests
    {
        const string SolutionPath = @"D:\temp\nhibernate-core-master\src\NHibernate.Everything.sln";

        [Test]
        public void Introspection_SearchForComplexityGt10_ApprovedList()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));

            var cancellationToken = cancellationTokenSource.Token;

            IEnumerable<Complexity> methodsWithCyclomaticComplexityGt10 = 
                new Introspection()
                    .SearchForComplexMethods(
                        solutionFile: SolutionPath,
                        maxAllowedCyclomaticComplexity: 10,
                        cancellationToken: cancellationToken);

            Approvals.Verify(JsonConvert.SerializeObject(methodsWithCyclomaticComplexityGt10, Formatting.Indented));
        }
    }
}
