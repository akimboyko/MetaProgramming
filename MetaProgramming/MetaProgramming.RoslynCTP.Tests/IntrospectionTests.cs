using System;
using System.Collections.Immutable;
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
        
        private CancellationTokenSource _cancellationTokenSource;

        [SetUp]
        public void SetUp()
        {
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        }

        [Test]
        public void Introspection_SearchForComplexityGt10_ApprovedList()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            IImmutableList<Complexity> methodsWithCyclomaticComplexityGt10 = 
                new Introspection()
                    .SearchForComplexMethods(
                        solutionFile: SolutionPath,
                        maxAllowedCyclomaticComplexity: 10,
                        cancellationToken: cancellationToken);

            var methodsWithCyclomaticComplexityGt10Results = methodsWithCyclomaticComplexityGt10
                .AsParallel()
                .GroupBy(complexity => complexity.TypeIdentifier)
                .OrderByDescending(@group => @group.Sum(complexity => complexity.NStatementSyntax))
                .ThenBy(@group => @group.First().FilePath)
                .Select(@group => @group
                                    .OrderByDescending(complexity => complexity.NStatementSyntax)
                                    .ThenBy(complexity => complexity.MethodIdentifier))
                .ToArray();

            Approvals.Verify(JsonConvert.SerializeObject(methodsWithCyclomaticComplexityGt10Results, Formatting.Indented));
        }

        [Test]
        public void Introspection_SearchForReturnNullStatements_ApprovedList()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            IImmutableList<ReturnNull> returnNullStatements =
                new Introspection()
                    .SearchForReturnNullStatements(
                        solutionFile: SolutionPath,
                        cancellationToken: cancellationToken);

            var orderedReturnNullStatements = returnNullStatements
                .AsParallel()
                .OrderBy(returnNull => returnNull.FilePath)
                .ThenBy(returnNull => returnNull.SourceLine)
                .ToArray();

            Approvals.Verify(JsonConvert.SerializeObject(orderedReturnNullStatements, Formatting.Indented));
        }
    }

    
}
