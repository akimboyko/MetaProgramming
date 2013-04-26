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
using System.Linq;
using Roslyn.Compilers.CSharp;

namespace CSharpToVisualBasicConverter.Utilities
{
    internal static class CSharpExtensions
    {
        public static IEnumerable<T> GetAncestorsOrThis<T>(this SyntaxNode node, bool allowStructuredTrivia = false)
            where T : SyntaxNode
        {
            var current = node;
            while (current != null)
            {
                if (current is T)
                {
                    yield return (T)current;
                }

                if (allowStructuredTrivia &&
                    current.IsStructuredTrivia &&
                    current.Parent == null)
                {
                    var structuredTrivia = (StructuredTriviaSyntax)current;
                    var parentTrivia = structuredTrivia.ParentTrivia;
                    current = parentTrivia.Token.Parent;
                }
                else
                {
                    current = current.Parent;
                }
            }
        }

        public static SyntaxNode GetParent(this SyntaxTree syntaxTree, SyntaxNode node)
        {
            return node != null ? node.Parent : null;
        }

        public static TypeSyntax GetVariableType(this VariableDeclaratorSyntax variable)
        {
            var parent = variable.Parent as VariableDeclarationSyntax;
            if (parent == null)
            {
                return null;
            }

            return parent.Type;
        }

        public static bool IsBreakableConstruct(this SyntaxNode node)
        {
            switch (node.Kind)
            {
                case SyntaxKind.DoStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                    return true;
            }

            return false;
        }

        public static bool IsContinuableConstruct(this SyntaxNode node)
        {
            switch (node.Kind)
            {
                case SyntaxKind.DoStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                    return true;
            }

            return false;
        }

        public static bool IsParentKind(this SyntaxNode node, SyntaxKind kind)
        {
            return node != null && node.Parent.IsKind(kind);
        }

        public static bool IsKind(this SyntaxNode node, SyntaxKind kind)
        {
            return node != null && node.Kind == kind;
        }
    }
}
