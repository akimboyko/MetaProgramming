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

using System.Collections.Generic;
using Roslyn.Compilers.CSharp;

namespace CSharpToVisualBasicConverter.Cleanup
{
    internal class NewLineCleanup : SyntaxRewriter
    {
        private readonly SyntaxTree syntaxTree;

        public NewLineCleanup(SyntaxTree syntaxTree)
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

            bool changed;

            do
            {
                changed = false;
                if ((token.HasLeadingTrivia && token.LeadingTrivia.Count >= 2) ||
                    (token.HasTrailingTrivia && token.TrailingTrivia.Count >= 2))
                {
                    var newLeadingTrivia = RemoveBlankLines(token.LeadingTrivia, ref changed);
                    var newTrailingTrivia = RemoveBlankLines(token.TrailingTrivia, ref changed);

                    if (changed)
                    {
                        token = token.WithLeadingTrivia(Syntax.TriviaList(newLeadingTrivia));
                        token = token.WithTrailingTrivia(Syntax.TriviaList(newTrailingTrivia));
                    }
                }
            }
            while (changed);

            return token;
        }

        private static List<SyntaxTrivia> RemoveBlankLines(SyntaxTriviaList trivia, ref bool changed)
        {
            var newTrivia = new List<SyntaxTrivia>();

            for (int i = 0; i < trivia.Count;)
            {
                var trivia1 = trivia[i];
                newTrivia.Add(trivia1);

                if (i < trivia.Count - 1)
                {
                    var trivia2 = trivia[i + 1];

                    if (trivia1.Kind == SyntaxKind.EndOfLineTrivia &&
                        trivia2.Kind == SyntaxKind.EndOfLineTrivia)
                    {
                        changed = true;
                        i += 2;
                        continue;
                    }
                }

                i++;
            }

            return newTrivia;
        }
    }
}