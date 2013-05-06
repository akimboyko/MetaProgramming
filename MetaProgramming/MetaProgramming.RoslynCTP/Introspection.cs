using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
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
        [SecurityCritical]
        public IEnumerable<Complexity> SearchForComplexMethods(string solutionFile, int maxAllowedCyclomaticComplexity, CancellationToken cancellationToken)
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
                    CalculateComplexity(syntaxRootAsync, complexityBag, cancellationToken));

            // throw an exception if more then 1 minute passed since start
            cancellationToken.ThrowIfCancellationRequested();

            return complexityBag.AsEnumerable();
        }

        // statements for independent paths through a program's source code
        private static readonly Func<StatementSyntax, bool> cyclomaticComplexityStatements =
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
        private static async void CalculateComplexity(
                                    Task<CommonSyntaxNode> syntaxRootAsync,
                                    ConcurrentBag<Complexity> complexityBag,
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
                                    nStatementSyntax = methodDeclaration.DescendantNodes()
                                                            .OfType<StatementSyntax>()
                                                            .Where(cyclomaticComplexityStatements)
                                                            .Count() + 1,
                                    FilePath = methodDeclaration.GetLocation().SourceTree.FilePath,
                                    SourceLine = methodDeclaration.GetLocation().SourceTree.GetLineSpan(methodDeclaration.Span, true, cancellationToken).StartLinePosition.Line
                                })
                    .Where(complexity => complexity.nStatementSyntax > 10)
                    .ToArray(),
                complexity =>
                {
                    complexityBag.Add(complexity);
                    cancellationToken.ThrowIfCancellationRequested();
                });
        }
    }
}
