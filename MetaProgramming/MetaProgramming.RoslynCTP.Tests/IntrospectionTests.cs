using System;
using System.Collections.Generic;
using System.Linq;
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
        // source code downloaded from https://github.com/nhibernate/nhibernate-core
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

            var methodsWithCyclomaticComplexityGt10Results = methodsWithCyclomaticComplexityGt10
                .GroupBy(complexity => complexity.TypeIdentifier)
                .OrderByDescending(@group => @group.Sum(complexity => complexity.nStatementSyntax))
                .ThenBy(@group => @group.First().FilePath)
                .Select(@group => @group
                                    .OrderByDescending(complexity => complexity.nStatementSyntax)
                                    .ThenBy(complexity => complexity.MethodIdentifier));

            Approvals.Verify(JsonConvert.SerializeObject(methodsWithCyclomaticComplexityGt10Results, Formatting.Indented));
        }
    }
}
