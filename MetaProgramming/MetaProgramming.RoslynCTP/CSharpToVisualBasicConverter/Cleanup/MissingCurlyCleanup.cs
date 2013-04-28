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

using Roslyn.Compilers.CSharp;

namespace CSharpToVisualBasicConverter.Cleanup
{
    internal class MissingCurlyCleanup : SyntaxRewriter
    {
        private readonly SyntaxTree syntaxTree;

        public MissingCurlyCleanup(SyntaxTree syntaxTree)
        {
            this.syntaxTree = syntaxTree;
        }

        public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
        {
            node = (IfStatementSyntax)base.VisitIfStatement(node);
            if (node.Statement.Kind == SyntaxKind.Block)
            {
                return node;
            }

            var block = Syntax.Block(statements: Syntax.List(node.Statement));
            return Syntax.IfStatement(
                node.IfKeyword,
                node.OpenParenToken,
                node.Condition,
                node.CloseParenToken,
                block,
                node.Else);
        }

        public override SyntaxNode VisitElseClause(ElseClauseSyntax node)
        {
            node = (ElseClauseSyntax)base.VisitElseClause(node);
            if (node.Statement.Kind == SyntaxKind.Block || node.Statement.Kind == SyntaxKind.IfStatement)
            {
                return node;
            }

            var block = Syntax.Block(statements: Syntax.List(node.Statement));
            return Syntax.ElseClause(
                node.ElseKeyword,
                block);
        }
    }
}
