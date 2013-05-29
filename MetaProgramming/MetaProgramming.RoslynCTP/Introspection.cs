using System.Collections.Generic;
using System.Threading;
using AfterProgramming;
using AfterProgramming.Model;

namespace MetaProgramming.RoslynCTP
{
    public class Introspection
    {
        public IEnumerable<Complexity> SearchForComplexMethods(
                string solutionFile,
                int maxAllowedCyclomaticComplexity,
                CancellationToken cancellationToken)
        {
            return new CyclomaticComplexity()
                        .SearchForComplexMethods(
                            solutionFile, maxAllowedCyclomaticComplexity, cancellationToken);
        }

        public IEnumerable<ReturnNull> SearchForReturnNullStatements(
                string solutionFile,
                CancellationToken cancellationToken)
        {
            return new ReturnStatement()
                        .SearchForReturnNullStatements(
                            solutionFile, cancellationToken);
        }
    }
}
