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

using System;
using System.Collections.Generic;
using System.Linq;
using CSharpToVisualBasicConverter.Utilities;
using Roslyn.Compilers;
using CS = Roslyn.Compilers.CSharp;
using VB = Roslyn.Compilers.VisualBasic;

namespace CSharpToVisualBasicConverter.Converting
{
    public partial class Converter
    {
        public VB.SyntaxNode Convert(
            CS.SyntaxTree syntaxTree,
            IDictionary<string, string> identifierMap = null,
            bool convertStrings = false)
        {
            var text = syntaxTree.GetText();
            var node = syntaxTree.GetRoot();

            var vbText = Convert(text, node, identifierMap, convertStrings);

            return VB.SyntaxTree.ParseText(vbText).GetRoot();
        }

        public string Convert(
            string text,
            IDictionary<string, string> identifierMap = null,
            bool convertStrings = false)
        {
            var parseFunctions = new List<Func<string, CS.SyntaxNode>>()
            {
                s => CS.Syntax.ParseExpression(s),
                s => CS.Syntax.ParseStatement(s),
            };

            foreach (var parse in parseFunctions)
            {
                var node = parse(text);
                var stringText = new StringText(text);

                if (!node.ContainsDiagnostics && node.FullSpan.Length == text.Length)
                {
                    return Convert(stringText, node, identifierMap, convertStrings);
                }
            }

            return Convert(CS.SyntaxTree.ParseText(text), identifierMap, convertStrings).ToFullString();
        }

        private static string Convert(
            IText text,
            CS.SyntaxNode node,
            IDictionary<string, string> identifierMap,
            bool convertStrings)
        {
            if (node is CS.StatementSyntax)
            {
                var nodeVisitor = new NodeVisitor(text, identifierMap, convertStrings);
                var statementVisitor = new StatementVisitor(nodeVisitor, text);
                var vbStatements = statementVisitor.Visit(node);

                return string.Join(Environment.NewLine, vbStatements.Select(s => VB.SyntaxExtensions.NormalizeWhitespace(s)));
            }
            else
            {
                var visitor = new NodeVisitor(text, identifierMap, convertStrings);
                var vbNode = visitor.Visit(node);

                return VB.SyntaxExtensions.NormalizeWhitespace(vbNode).ToFullString();
            }
        }

        private static IEnumerable<T> Empty<T>()
        {
            return System.Linq.Enumerable.Empty<T>();
        }

        private static VB.SeparatedSyntaxList<T> SeparatedList<T>(T value)
            where T : VB.SyntaxNode
        {
            return VB.Syntax.SeparatedList(value);
        }

        private static VB.SeparatedSyntaxList<T> SeparatedNewLineList<T>(IEnumerable<T> nodes)
            where T : VB.SyntaxNode
        {
            var nodesList = nodes as IList<T> ?? nodes.ToList();
            var builder = new List<VB.SyntaxNodeOrToken>();
            foreach (var node in nodes)
            {
                builder.Add(node);
                builder.Add(StatementTerminator());
            }

            return VB.Syntax.SeparatedList<T>(builder);
        }

        private static VB.SyntaxTriviaList TriviaList(IEnumerable<VB.SyntaxTrivia> list)
        {
            return VB.Syntax.TriviaList(list.Aggregate(new List<VB.SyntaxTrivia>(), (builder, trivia) => { builder.Add(trivia); return builder; }));
        }

        private static VB.SyntaxList<T> List<T>(T node)
            where T : VB.SyntaxNode
        {
            if (node == null)
            {
                return VB.Syntax.List<T>();
            }
            else
            {
                return VB.Syntax.List(node);
            }
        }

        private static VB.SyntaxList<T> List<T>(IEnumerable<T> nodes)
            where T : VB.SyntaxNode
        {
            return VB.Syntax.List<T>(nodes.Aggregate(new List<T>(), (list, node) => { list.Add(node); return list; }));
        }

        private static VB.SeparatedSyntaxList<T> SeparatedCommaList<T>(IEnumerable<T> nodes)
            where T : VB.SyntaxNode
        {
            var nodesList = nodes as IList<T> ?? nodes.ToList();
            var builder = new List<VB.SyntaxNodeOrToken>();
            bool first = true;
            foreach (var node in nodes)
            {
                if (!first)
                {
                    builder.Add(VB.Syntax.Token(VB.SyntaxKind.CommaToken));
                }

                first = false;
                builder.Add(node);
            }

            return VB.Syntax.SeparatedList<T>(builder);
        }

        private static VB.SeparatedSyntaxList<T> SingleSeparatedList<T>(T node)
            where T : VB.SyntaxNode
        {
            if (node == null)
            {
                return VB.Syntax.SeparatedList<T>();
            }
            else
            {
                return VB.Syntax.SeparatedList(node);
            }
        }

        private static VB.SyntaxToken StatementTerminator()
        {
            return VB.Syntax.Token(VB.SyntaxKind.StatementTerminatorToken);
        }

        private static string RemoveNewLines(string text)
        {
            return text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static string ReplaceNewLines(string text)
        {
            return text.Replace("\r\n", "\\r\\n").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string CreateCouldNotBeConvertedText(string text, Type type)
        {
            return "'" + RemoveNewLines(text) + "' could not be converted to a " + type.Name;
        }

        private static string CreateCouldNotBeConvertedComment(string text, Type type)
        {
            return "' " + CreateCouldNotBeConvertedText(text, type);
        }

        private static string CreateCouldNotBeConvertedString(string text, Type type)
        {
            return "\"" + CreateCouldNotBeConvertedText(text, type) + "\"";
        }

        private static VB.StatementSyntax CreateBadStatement(string text, Type type)
        {
            var comment = CreateCouldNotBeConvertedComment(text, type);
            var trivia = VB.Syntax.CommentTrivia(comment);

            var token = VB.Syntax.Token(trivia, VB.SyntaxKind.EmptyToken);
            return VB.Syntax.EmptyStatement(token);
        }

        private static VB.StatementSyntax CreateBadStatement(CS.SyntaxNode node, NodeVisitor visitor)
        {
            var leadingTrivia = node.GetFirstToken(includeSkipped: true).LeadingTrivia.SelectMany(visitor.VisitTrivia);
            var trailingTrivia = node.GetLastToken(includeSkipped: true).TrailingTrivia.SelectMany(visitor.VisitTrivia);

            var comment = CreateCouldNotBeConvertedComment(node.ToString(), typeof(VB.StatementSyntax));
            leadingTrivia = leadingTrivia.Concat(
                VB.Syntax.CommentTrivia(comment));

            var token = VB.Syntax.Token(TriviaList(leadingTrivia), VB.SyntaxKind.EmptyToken, trailing: TriviaList(trailingTrivia));
            return VB.Syntax.EmptyStatement(token);
        }

        private static VB.StructuredTriviaSyntax CreateBadDirective(CS.SyntaxNode node, NodeVisitor visitor)
        {
            var leadingTrivia = node.GetFirstToken(includeSkipped: true).LeadingTrivia.SelectMany(visitor.VisitTrivia).Where(t => t.Kind != VB.SyntaxKind.EndOfLineTrivia);
            var trailingTrivia = node.GetLastToken(includeSkipped: true).TrailingTrivia.SelectMany(visitor.VisitTrivia).Where(t => t.Kind != VB.SyntaxKind.EndOfLineTrivia);

            var comment = CreateCouldNotBeConvertedComment(node.ToString(), typeof(VB.StatementSyntax));
            leadingTrivia = leadingTrivia.Concat(
                VB.Syntax.CommentTrivia(comment));

            var token = VB.Syntax.Token(TriviaList(leadingTrivia), VB.SyntaxKind.HashToken, trailing: TriviaList(trailingTrivia), text: "");
            return VB.Syntax.DirectiveTrivia(VB.Syntax.BadDirective(token), VB.Syntax.Token(VB.SyntaxKind.StatementTerminatorToken, text: ""));
        }
    }
}
