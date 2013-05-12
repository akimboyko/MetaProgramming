using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ApprovalTests;
using ApprovalTests.Reporters;
using FluentAssertions;
using MetaProgramming.RoslynCTP.Model;
using MetaProgramming.RoslynCTP.Tests.Strategy;
using NUnit.Framework;
using Newtonsoft.Json;
using Approvals = ApprovalTests.Approvals;

namespace MetaProgramming.RoslynCTP.Tests
{
    //[TestFixture(typeof(IntegrationIntrospectionFixture))]
    [TestFixture(typeof(CodeSmellsIntrospectionFixture))]
    [UseReporter(typeof(DiffReporter))]
    public class IntrospectionTests<TIntrospectionFixture>
        where TIntrospectionFixture : IIntrospectionFixture, new()
    {
        private readonly TIntrospectionFixture _strategy = new TIntrospectionFixture();

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

            IEnumerable<Complexity> methodsWithCyclomaticComplexityGt10 = 
                new Introspection()
                    .SearchForComplexMethods(
                        solutionFile: _strategy.GetSolutionPath(),
                        maxAllowedCyclomaticComplexity: 10,
                        cancellationToken: cancellationToken);

            methodsWithCyclomaticComplexityGt10.Should()
                            .NotBeNull()
                            .And.BeOfType<ImmutableList<Complexity>>()
                            .And.NotBeEmpty();

            var methodsWithCyclomaticComplexityGt10Results = 
                methodsWithCyclomaticComplexityGt10
                    .GroupBy(complexity => complexity.TypeIdentifier)
                    .OrderByDescending(@group => @group.Sum(complexity => complexity.NStatementSyntax))
                    .ThenBy(@group => @group.First().FilePath)
                    .Select(@group => @group
                                        .OrderByDescending(complexity => complexity.NStatementSyntax)
                                        .ThenBy(complexity => complexity.MethodIdentifier))
                .ToArray();

            ApprovalsVerify(methodsWithCyclomaticComplexityGt10Results);
        }

        [Test]
        public void Introspection_SearchForReturnNullStatements_ApprovedList()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            IEnumerable<ReturnNull> returnNullStatements =
                new Introspection()
                    .SearchForReturnNullStatements(
                        solutionFile: _strategy.GetSolutionPath(),
                        cancellationToken: cancellationToken);

            returnNullStatements.Should()
                            .NotBeNull()
                            .And.BeOfType<ImmutableList<ReturnNull>>()
                            .And.NotBeEmpty();

            var orderedReturnNullStatements = 
                returnNullStatements
                    .OrderBy(returnNull => returnNull.FilePath)
                    .ThenBy(returnNull => returnNull.SourceLine)
                    .ToArray();

            ApprovalsVerify(orderedReturnNullStatements);
        }

        private void ApprovalsVerify<T>(IEnumerable<T> records)
        {
            var serializeObject = JsonConvert.SerializeObject(records, Formatting.Indented);
            Approvals.Verify(new ApprovalTextWriter(serializeObject), new StrategyNamer(_strategy.GetTestType()),
                             new DiffReporter());
        }
    }
}
