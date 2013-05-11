using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ApprovalTests.Reporters;
using MetaProgramming.RoslynCTP.Model;
using MetaProgramming.RoslynCTP.Tests.Strategy;
using NUnit.Framework;
using Newtonsoft.Json;
using Approvals = ApprovalTests.Approvals;

namespace MetaProgramming.RoslynCTP.Tests
{
    [TestFixture(typeof(IntegrationIntrospectionFixture))]
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
            Approvals.RegisterDefaultNamerCreation(() => new StrategyNamer(_strategy.GetTestType()));

            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        }

        [Test]
        public void Introspection_SearchForComplexityGt10_ApprovedList()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            IImmutableList<Complexity> methodsWithCyclomaticComplexityGt10 = 
                new Introspection()
                    .SearchForComplexMethods(
                        solutionFile: _strategy.GetSolutionPath(),
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
                        solutionFile: _strategy.GetSolutionPath(),
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
