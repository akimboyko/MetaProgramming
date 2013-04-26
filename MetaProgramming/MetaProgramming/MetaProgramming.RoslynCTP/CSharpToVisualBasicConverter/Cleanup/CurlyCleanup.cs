// *********************************************************
//
// Copyright © Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0 
//
// THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES
// OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
// INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES
// OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache 2 License for the specific language
// governing permissions and limitations under the License.
//
// *********************************************************

using System.Linq;
using Roslyn.Compilers.CSharp;

namespace CSharpToVisualBasicConverter.Cleanup
{
    internal class CurlyCleanup : SyntaxRewriter
    {
        private readonly SyntaxTree syntaxTree;

        public CurlyCleanup(SyntaxTree syntaxTree)
        {
            this.syntaxTree = syntaxTree;
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            token = base.VisitToken(token);
            if (token.IsMissing)
            {
                return token;
            }

            if (token.Kind != SyntaxKind.CloseBraceToken)
            {
                return token;
            }

            var nextToken = token.GetNextToken(includeSkipped: true);

            var tokenLine = syntaxTree.GetText().GetLineNumberFromPosition(token.Span.Start);
            var nextTokenLine = syntaxTree.GetText().GetLineNumberFromPosition(nextToken.Span.Start);
            var nextTokenIsCloseBrace = nextToken.Kind == SyntaxKind.CloseBraceToken;

            var expectedDiff = nextTokenIsCloseBrace ? 1 : 2;
            if (nextTokenLine == tokenLine + expectedDiff)
            {
                return token;
            }

            var nonNewLineTrivia = token.TrailingTrivia.Where(t => t.Kind != SyntaxKind.EndOfLineTrivia);
            var newTrivia = nonNewLineTrivia.Concat(Enumerable.Repeat(Syntax.EndOfLine("\r\n"), expectedDiff));

            return token.WithTrailingTrivia(Syntax.TriviaList(newTrivia));
        }
    }
}