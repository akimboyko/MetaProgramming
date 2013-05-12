using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
        public IImmutableList<Complexity> SearchForComplexMethods(
                                            string solutionFile,
                                            int maxAllowedCyclomaticComplexity,
                                            CancellationToken cancellationToken)
        {
            var calculateComplexity = new Action<Task<CommonSyntaxNode>, string, ConcurrentBag<Complexity>, CancellationToken>((task, solutionDir, bag, token) => CalculateComplexity(task, solutionDir, bag, maxAllowedCyclomaticComplexity, token));

            return SearchFor(
                solutionFile: solutionFile,
                searchAction: calculateComplexity,
                cancellationToken: cancellationToken);
        }

        public IImmutableList<ReturnNull> SearchForReturnNullStatements(
                string solutionFile,
                CancellationToken cancellationToken)
        {
            return SearchFor(
                solutionFile: solutionFile,
                searchAction: new Action<Task<CommonSyntaxNode>, string, ConcurrentBag<ReturnNull>, CancellationToken>(
                            GetReturnNullStatements),
                cancellationToken: cancellationToken);
        }

        private static IImmutableList<TResult> SearchFor<TResult>(
                string solutionFile,
                Action<Task<CommonSyntaxNode>, string, ConcurrentBag<TResult>, CancellationToken> searchAction,
                CancellationToken cancellationToken)
        {
            var concurrentBagWithResults = new ConcurrentBag<TResult>();

            // load workspace, i.e. solution from Visual Studio
            using (var workspace = Workspace.LoadSolution(solutionFile))
            {
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

                var solutionDir = Path.GetDirectoryName(solutionFile);

                // calculate complexity for all methods in parallel
                Parallel.ForEach(
                    asyncSyntexRoots,
                    new ParallelOptions
                        {
                            CancellationToken = cancellationToken
                        },
                    syntaxRootAsync =>
                    searchAction(syntaxRootAsync, solutionDir, concurrentBagWithResults, cancellationToken));

                // throw an exception if more then 1 minute passed since start
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            return ImmutableList.Create(concurrentBagWithResults.AsEnumerable());
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
                    .And(s => 
                                (s is ReturnStatementSyntax && (s as ReturnStatementSyntax).Expression.Kind == SyntaxKind.NullLiteralExpression)
                                || ((s as ReturnStatementSyntax).Expression.Kind == SyntaxKind.DefaultExpression)
                                    && (((s as ReturnStatementSyntax).Expression as DefaultExpressionSyntax).Type != null)
                                )
                    .Compile();

        // process descendant nodes of syntaxRoot
        private static readonly Func<StatementSyntax, bool> YieldReturnNullStatement =
                PredicateBuilder
                    .True<StatementSyntax>()
                    .And(s => s is YieldStatementSyntax)
                    .And(s => (s as YieldStatementSyntax).Expression != null)
                    .And(s =>
                                (s is YieldStatementSyntax && (s as YieldStatementSyntax).Expression.Kind == SyntaxKind.NullLiteralExpression)
                                || ((s as YieldStatementSyntax).Expression.Kind == SyntaxKind.DefaultExpression)
                                    && (((s as YieldStatementSyntax).Expression as DefaultExpressionSyntax).Type != null)
                                )
                    .Compile();

        private static async void CalculateComplexity(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    string solutionDir,
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
                                    FilePath = GetRelativeFilePath(solutionDir, methodDeclaration.GetLocation().SourceTree.FilePath),
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

        // statements for `return null;`, `return default(object);`,
        //                `yield return null;`, `yield return default(object);`
        private static async void GetReturnNullStatements(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    string solutionDir,
                                    ConcurrentBag<ReturnNull> returnNullBag,
                                    CancellationToken cancellationToken)
        {
            var returnNulls = GetReturnNullStatements<ReturnStatementSyntax>(syntaxRootAsync, solutionDir,
                                                           ReturnNullStatement, cancellationToken);

            var yieldReturnNull = GetReturnNullStatements<YieldStatementSyntax>(syntaxRootAsync, solutionDir,
                                                           YieldReturnNullStatement, cancellationToken);

            Array.ForEach((await returnNulls).ToArray(), returnNullBag.Add);
            Array.ForEach((await yieldReturnNull).ToArray(), returnNullBag.Add);
        }

        private static async Task<IEnumerable<ReturnNull>> GetReturnNullStatements<TReturnStatementSyntax>(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    string solutionDir,
                                    Func<TReturnStatementSyntax, bool> statement,
                                    CancellationToken cancellationToken)
            where TReturnStatementSyntax : StatementSyntax
        {
            return (await syntaxRootAsync)
                    .DescendantNodes()
                    .AsParallel()
                    .WithCancellation(cancellationToken)
                    .OfType<TReturnStatementSyntax>()
                    .Where(statement)
                    .Select(returnNull =>
                            new ReturnNull
                            {
                                TypeIdentifier = GetParentSyntax<TypeDeclarationSyntax>(returnNull).Identifier.ValueText,
                                SourcesSample = returnNull.ToString(),
                                FilePath = GetRelativeFilePath(solutionDir, returnNull.GetLocation().SourceTree.FilePath),
                                SourceLine = returnNull
                                                 .GetLocation().SourceTree
                                                 .GetLineSpan(returnNull.Span, true, cancellationToken)
                                                 .StartLinePosition.Line + 1
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

        private static string GetRelativeFilePath(string solutionDir, string filePath)
        {
            var relativePath = new Uri(Path.GetDirectoryName(solutionDir)).MakeRelativeUri(new Uri(Path.GetDirectoryName(filePath))).ToString();

            return Path.Combine(relativePath, Path.GetFileName(filePath))
                       .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
