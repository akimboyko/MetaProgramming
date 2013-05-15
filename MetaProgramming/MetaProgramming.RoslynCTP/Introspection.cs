using System;
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
        public IEnumerable<Complexity> SearchForComplexMethods(
                string solutionFile,
                int maxAllowedCyclomaticComplexity,
                CancellationToken cancellationToken)
        {
            var calculateComplexity = new Func<Task<CommonSyntaxNode>, Task<ISemanticModel>, string, CancellationToken, Task<IEnumerable<Complexity>>>((syntaxRootAsync, semanticModelAsync, solutionDir, token) => CalculateComplexity(syntaxRootAsync, semanticModelAsync, solutionDir, maxAllowedCyclomaticComplexity, token));

            return SearchFor(
                solutionFile: solutionFile,
                searchFunc: calculateComplexity,
                cancellationToken: cancellationToken);
        }

        public IEnumerable<ReturnNull> SearchForReturnNullStatements(
                string solutionFile,
                CancellationToken cancellationToken)
        {
            var getReturnNullStatements = new Func<Task<CommonSyntaxNode>, Task<ISemanticModel>, string, CancellationToken, Task<IEnumerable<ReturnNull>>>((syntaxRootAsync, semanticModelAsync, solutionDir, token) => GetReturnNullStatements(syntaxRootAsync, semanticModelAsync, solutionDir, token));

            return SearchFor(
                solutionFile: solutionFile,
                searchFunc: getReturnNullStatements,
                cancellationToken: cancellationToken);
        }

        private static IImmutableList<TResult> SearchFor<TResult>(
                string solutionFile,
                Func<Task<CommonSyntaxNode>, Task<ISemanticModel>, string, CancellationToken, Task<IEnumerable<TResult>>> searchFunc,
                CancellationToken cancellationToken)
        {
            IEnumerable<TResult> results;

            // load workspace, i.e. solution from Visual Studio
            using (var workspace = Workspace.LoadSolution(solutionFile))
            {
                // save a reference to original state
                var origianlSolution = workspace.CurrentSolution;

                // Get absolute path to solution directory
                var solutionDir = Path.GetDirectoryName(solutionFile);

                // build syntax root asynchronously in parallel for all documents from all projects
                // then get all syntax nodes
                results =
                    origianlSolution
                        .Projects
                        .AsParallel()
                            .AsUnordered()
                        .WithCancellation(cancellationToken)
                        .SelectMany(project => project.Documents)
                        .Select(document =>
                            new 
                            {
                                syntaxRootAsync = document.GetSyntaxRootAsync(cancellationToken),
                                semanticModelAsync = document.GetSemanticModelAsync(cancellationToken)
                            })
                        .SelectMany(model => searchFunc(model.syntaxRootAsync, model.semanticModelAsync, solutionDir, cancellationToken).Result)
                        .ToList();

                // throw an exception if more then 1 minute passed since start
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ImmutableList.Create(results);
        }

        // statements for independent paths through a program's source code
        // TODO: add clauses: else, catch, …
        private static readonly Func<StatementSyntax, bool> CyclomaticComplexityStatements =
                PredicateBuilder
                    .False<StatementSyntax>()
                    .Or(s => s is IfStatementSyntax)
                    .Or(s => s is SwitchStatementSyntax)
                    .Or(s => s is DoStatementSyntax)
                    .Or(s => s is WhileStatementSyntax)
                    .Or(s => s is ForStatementSyntax)
                    .Or(s => s is ForEachStatementSyntax)
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


        private static async Task<bool> IsExpressionOfReferenceType(StatementSyntax statement, Task<ISemanticModel> semanticModelAsync)
        {
            ExpressionSyntax expressionSyntax;

            if (statement is ReturnStatementSyntax)
            {
                expressionSyntax = (statement as ReturnStatementSyntax).Expression;
            }
            else if (statement is YieldStatementSyntax)
            {
                expressionSyntax = (statement as YieldStatementSyntax).Expression;
            }
            else
            {
                throw new ArgumentException("Not supported StatementSyntax", "statement");
            }

            var semanticModel = await semanticModelAsync;

            return expressionSyntax != null &&
                    (semanticModel.GetTypeInfo(expressionSyntax).Type == null
                        || semanticModel.GetTypeInfo(expressionSyntax).Type.IsReferenceType);
        }

        private static async Task<IEnumerable<Complexity>> CalculateComplexity(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    Task<ISemanticModel> semanticModelAsync,
                                    string solutionDir,
                                    int maxAllowedCyclomaticComplexity,
                                    CancellationToken cancellationToken)
        {
            return (await syntaxRootAsync)
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(methodDeclaration =>
                        new Complexity
                            {
                                TypeIdentifier = ((TypeDeclarationSyntax) methodDeclaration.Parent).Identifier.ValueText,
                                MethodIdentifier = methodDeclaration.Identifier.ValueText,
                                SourcesSample = methodDeclaration.ToString(),
                                NStatementSyntax = methodDeclaration.DescendantNodes()
                                                                    .OfType<StatementSyntax>()
                                                                    .Where(CyclomaticComplexityStatements)
                                                                    .Count() + 1,
                                FilePath =
                                    GetRelativeFilePath(solutionDir, methodDeclaration.GetLocation().SourceTree.FilePath),
                                SourceLine =
                                    methodDeclaration.GetLocation()
                                                     .SourceTree.GetLineSpan(methodDeclaration.Span, true,
                                                                             cancellationToken)
                                                     .StartLinePosition.Line
                            })
                .Where(complexity => complexity.NStatementSyntax > maxAllowedCyclomaticComplexity);
        }

        // statements for `return null;`, `return default(object);`,
        //                `yield return null;`, `yield return default(object);`
        private static async Task<IEnumerable<ReturnNull>> GetReturnNullStatements(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    Task<ISemanticModel> semanticModelAsync,
                                    string solutionDir,
                                    CancellationToken cancellationToken)
        {
            var returnNulls = GetReturnNullStatements<ReturnStatementSyntax>(
                                                            syntaxRootAsync, semanticModelAsync,
                                                            solutionDir, ReturnNullStatement, cancellationToken);

            var yieldReturnNull = GetReturnNullStatements<YieldStatementSyntax>(
                                                            syntaxRootAsync, semanticModelAsync,
                                                            solutionDir, YieldReturnNullStatement, cancellationToken);

            var results = new List<ReturnNull>();

            results.AddRange(await returnNulls);
            results.AddRange(await yieldReturnNull);

            return results;
        }

        private static async Task<IEnumerable<ReturnNull>> GetReturnNullStatements<TReturnStatementSyntax>(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    Task<ISemanticModel> semanticModelAsync,
                                    string solutionDir,
                                    Func<TReturnStatementSyntax, bool> statement,
                                    CancellationToken cancellationToken)
            where TReturnStatementSyntax : StatementSyntax
        {
            return (await syntaxRootAsync)
                    .DescendantNodes()
                    .OfType<TReturnStatementSyntax>()
                    .Where(statement)
                    .Where(returnNull => IsExpressionOfReferenceType(returnNull, semanticModelAsync).Result)
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
            var relativePath = new Uri(Path.GetDirectoryName(solutionDir))
                                        .MakeRelativeUri(
                                            new Uri(Path.GetDirectoryName(filePath))).ToString();

            return Path.Combine(relativePath, Path.GetFileName(filePath))
                       .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
