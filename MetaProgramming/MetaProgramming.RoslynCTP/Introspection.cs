using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetaProgramming.RoslynCTP.Internal;
using MetaProgramming.RoslynCTP.Model;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using Roslyn.Services;

namespace MetaProgramming.RoslynCTP
{
    public class Introspection
    {
        public IEnumerable<Complexity> SearchForComplexMethods(
                                            string solutionFile,
                                            int maxAllowedCyclomaticComplexity,
                                            CancellationToken cancellationToken)
        {
            // load workspace, i.e. solution from Visual Studio
            var workspace = Workspace.LoadSolution(solutionFile);

            // save a reference to original state
            var origianlSolution = workspace.CurrentSolution;

            // build syntax root asynchronously in parallel for all documents from all projects 
            var asyncSyntexRoots =
                origianlSolution
                    .Projects
                    .AsParallel()
                        .AsUnordered()
                    .SelectMany(project => project.Documents)
                    .Select(document => document.GetSyntaxRootAsync(cancellationToken))
                    .ToArray();

            var complexityBag = new ConcurrentBag<Complexity>();

            // calculate complexity for all methods in parallel
            Parallel.ForEach(
                asyncSyntexRoots,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken
                },
                syntaxRootAsync =>
                    CalculateComplexity(syntaxRootAsync, complexityBag, maxAllowedCyclomaticComplexity, cancellationToken));

            // throw an exception if more then 1 minute passed since start
            cancellationToken.ThrowIfCancellationRequested();

            return complexityBag.AsEnumerable();
        }

        public IEnumerable<ReturnNull> SearchForReturnNullStatements(
                string solutionFile,
                CancellationToken cancellationToken)
        {
            // load workspace, i.e. solution from Visual Studio
            var workspace = Workspace.LoadSolution(solutionFile);

            // save a reference to original state
            var origianlSolution = workspace.CurrentSolution;

            // build syntax root asynchronously in parallel for all documents from all projects 
            var asyncSyntexRoots =
                origianlSolution
                    .Projects
                    .AsParallel()
                        .AsUnordered()
                    .SelectMany(project => project.Documents)
                    .Select(document => document.GetSyntaxRootAsync(cancellationToken))
                    .ToArray();

            var complexityBag = new ConcurrentBag<ReturnNull>();

            // calculate complexity for all methods in parallel
            Parallel.ForEach(
                asyncSyntexRoots,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken
                },
                syntaxRootAsync =>
                    GetReturnNullStatements(syntaxRootAsync, complexityBag, cancellationToken));

            // throw an exception if more then 1 minute passed since start
            cancellationToken.ThrowIfCancellationRequested();

            return complexityBag.AsEnumerable();
        }

        // statements for independent paths through a program's source code
        private static readonly Func<StatementSyntax, bool> CyclomaticComplexityStatements =
                PredicateBuilder
                    .False<StatementSyntax>()
                    .Or(s => s is DoStatementSyntax)
                    .Or(s => s is ForEachStatementSyntax)
                    .Or(s => s is ForStatementSyntax)
                    .Or(s => s is IfStatementSyntax)
                    .Or(s => s is SwitchStatementSyntax)
                    .Or(s => s is UsingStatementSyntax)
                    .Or(s => s is WhileStatementSyntax)
                        .Compile();

        // process descendant nodes of syntaxRoot
        private static readonly Func<StatementSyntax, bool> ReturnNullStatement =
                PredicateBuilder
                    .True<StatementSyntax>()
                    .And(s => s is ReturnStatementSyntax)
                    .And(s => (s as ReturnStatementSyntax).Expression != null)
                    .And(s => (s as ReturnStatementSyntax).Expression.Kind == SyntaxKind.NullLiteralExpression)
                    .Compile();

        private static async void CalculateComplexity(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    ConcurrentBag<Complexity> complexityBag,
                                    int maxAllowedCyclomaticComplexity,
                                    CancellationToken cancellationToken)
        {
            Array.ForEach(
                (await syntaxRootAsync)
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Select(methodDeclaration =>
                                new Complexity
                                {
                                    TypeIdentifier = ((TypeDeclarationSyntax)methodDeclaration.Parent).Identifier.ValueText,
                                    MethodIdentifier = methodDeclaration.Identifier.ValueText,
                                    SourcesSample = methodDeclaration.ToString(),
                                    NStatementSyntax = methodDeclaration.DescendantNodes()
                                                            .OfType<StatementSyntax>()
                                                            .Where(CyclomaticComplexityStatements)
                                                            .Count() + 1,
                                    FilePath = methodDeclaration.GetLocation().SourceTree.FilePath,
                                    SourceLine = methodDeclaration.GetLocation().SourceTree.GetLineSpan(methodDeclaration.Span, true, cancellationToken).StartLinePosition.Line
                                })
                    .Where(complexity => complexity.NStatementSyntax > maxAllowedCyclomaticComplexity)
                    .ToArray(),
                complexity =>
                {
                    complexityBag.Add(complexity);
                    cancellationToken.ThrowIfCancellationRequested();
                });
        }

        // statements for `return null;`
        private static async void GetReturnNullStatements(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    ConcurrentBag<ReturnNull> returnNullBag,
                                    CancellationToken cancellationToken)
        {
            Array.ForEach(
                (await syntaxRootAsync)
                    .DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .Where(ReturnNullStatement)
                    .Select(returnNull =>
                            new ReturnNull
                                {
                                    TypeIdentifier = GetParentSyntax<TypeDeclarationSyntax>(returnNull).Identifier.ValueText,
                                    SourcesSample = returnNull.ToString(),
                                    FilePath = returnNull.GetLocation().SourceTree.FilePath,
                                    SourceLine = returnNull
                                                     .GetLocation().SourceTree
                                                     .GetLineSpan(returnNull.Span, true, cancellationToken)
                                                     .StartLinePosition.Line + 1
                                })
                    .ToArray(),
                returnNull =>
                {
                    returnNullBag.Add(returnNull);
                    cancellationToken.ThrowIfCancellationRequested();
                });
        }

        // process descendant nodes of syntaxRoot
        private static TDeclarationSyntax GetParentSyntax<TDeclarationSyntax>(SyntaxNode statementSyntax)
                            where TDeclarationSyntax : MemberDeclarationSyntax
        {
            SyntaxNode statement = statementSyntax;
            while (statement != null && !(statement is TDeclarationSyntax))
            {
                statement = statement.Parent;
            }

            if (statement == null || !(statement is TDeclarationSyntax))
            {
                throw new Exception(string.Format("Can't find parent {0} node", typeof(TDeclarationSyntax)));
            }

            return (TDeclarationSyntax)statement;
        }
    }
}
