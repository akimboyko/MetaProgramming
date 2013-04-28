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
using Roslyn.Compilers.Common;
using CS = Roslyn.Compilers.CSharp;
using VB = Roslyn.Compilers.VisualBasic;

namespace CSharpToVisualBasicConverter.Converting
{
    public partial class Converter
    {
        private partial class StatementVisitor : CS.SyntaxVisitor<VB.SeparatedSyntaxList<VB.StatementSyntax>>
        {
            private readonly NodeVisitor nodeVisitor;
            private readonly IText text;

            public StatementVisitor(NodeVisitor nodeVisitor, IText text)
            {
                this.nodeVisitor = nodeVisitor;
                this.text = text;
            }

            public IEnumerable<VB.StatementSyntax> VisitStatementEnumerable(CS.StatementSyntax node)
            {
                return Visit(node);
            }

            public VB.SeparatedSyntaxList<VB.StatementSyntax> VisitStatement(CS.StatementSyntax node)
            {
                return Visit(node);
            }

            private static VB.StatementSyntax ConvertToStatement(VB.SyntaxNode node)
            {
                if (node == null)
                {
                    return null;
                }
                else if (node is VB.StatementSyntax)
                {
                    return (VB.StatementSyntax)node;
                }
                else if (node is VB.InvocationExpressionSyntax)
                {
                    return VB.Syntax.CallStatement(
                        default(VB.SyntaxToken),
                        (VB.InvocationExpressionSyntax)node);
                }
                else
                {
                    // can happen in error scenarios
                    return CreateBadStatement(((CommonSyntaxNode)node).ToFullString(), typeof(VB.StatementSyntax));
                }
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitBlock(CS.BlockSyntax node)
            {
                var statements = node.Statements.SelectMany(VisitStatementEnumerable).ToList();

                if (node.IsParentKind(CS.SyntaxKind.ConstructorDeclaration))
                {
                    var constructor = (CS.ConstructorDeclarationSyntax)node.Parent;
                    if (constructor.Initializer != null)
                    {
                        var initializer = nodeVisitor.Visit<VB.StatementSyntax>(constructor.Initializer);
                        statements.Insert(0, initializer);
                    }
                }

                return SeparatedNewLineList(statements);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitLocalDeclarationStatement(CS.LocalDeclarationStatementSyntax node)
            {
                var leadingTrivia = TriviaList(node.GetFirstToken(includeSkipped: true).LeadingTrivia.SelectMany(nodeVisitor.VisitTrivia));

                var token = node.Modifiers.Any(t => t.Kind == CS.SyntaxKind.ConstKeyword)
                    ? VB.Syntax.Token(leadingTrivia, VB.SyntaxKind.ConstKeyword)
                    : VB.Syntax.Token(leadingTrivia, VB.SyntaxKind.DimKeyword);

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.FieldDeclaration(
                        new VB.SyntaxList<VB.AttributeListSyntax>(),
                        token,
                        SeparatedCommaList(node.Declaration.Variables.Select(nodeVisitor.Visit<VB.VariableDeclaratorSyntax>))));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitReturnStatement(CS.ReturnStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.ReturnStatement(
                        expression: nodeVisitor.VisitExpression(node.Expression)));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitExpressionStatement(CS.ExpressionStatementSyntax node)
            {
                return SeparatedList(
                    nodeVisitor.VisitStatement(node.Expression));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitIfStatement(CS.IfStatementSyntax node)
            {
                var ifBegin = VB.Syntax.IfStatement(
                    ifOrElseIfKeyword: VB.Syntax.Token(VB.SyntaxKind.IfKeyword),
                    condition: nodeVisitor.VisitExpression(node.Condition));

                var ifPart = VB.Syntax.IfPart(
                    ifBegin,
                    StatementTerminator(),
                    Visit(node.Statement));

                var elseIfParts = new List<VB.IfPartSyntax>();
                var currentElseClause = node.Else;
                while (currentElseClause != null)
                {
                    if (currentElseClause.Statement.Kind != CS.SyntaxKind.IfStatement)
                    {
                        break;
                    }

                    var nestedIf = (CS.IfStatementSyntax)currentElseClause.Statement;
                    currentElseClause = nestedIf.Else;

                    var elseIfBegin = VB.Syntax.IfStatement(
                        ifOrElseIfKeyword: VB.Syntax.Token(VB.SyntaxKind.ElseIfKeyword),
                        condition: nodeVisitor.VisitExpression(nestedIf.Condition));
                    var elseIfPart = VB.Syntax.IfPart(
                        elseIfBegin,
                        StatementTerminator(),
                        Visit(nestedIf.Statement));
                    elseIfParts.Add(elseIfPart);
                }

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.MultiLineIfBlock(
                        ifPart,
                        List<VB.IfPartSyntax>(elseIfParts),
                        currentElseClause == null ? null : nodeVisitor.Visit<VB.ElsePartSyntax>(currentElseClause),
                        VB.Syntax.EndIfStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitSwitchStatement(CS.SwitchStatementSyntax node)
            {
                var begin = VB.Syntax.SelectStatement(
                    expression: nodeVisitor.VisitExpression(node.Expression));

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.SelectBlock(
                        begin,
                        StatementTerminator(),
                        List(node.Sections.Select(nodeVisitor.Visit<VB.CaseBlockSyntax>)),
                        VB.Syntax.EndSelectStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitThrowStatement(CS.ThrowStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.ThrowStatement(
                        expression: nodeVisitor.VisitExpression(node.Expression)));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitBreakStatement(CS.BreakStatementSyntax node)
            {
                return SeparatedList(VisitBreakStatementWorker(node));
            }

            private VB.StatementSyntax VisitBreakStatementWorker(CS.BreakStatementSyntax node)
            {
                foreach (var parent in node.GetAncestorsOrThis<CS.SyntaxNode>())
                {
                    if (parent.IsBreakableConstruct())
                    {
                        switch (parent.Kind)
                        {
                            case CS.SyntaxKind.DoStatement:
                                return VB.Syntax.ExitDoStatement();
                            case CS.SyntaxKind.WhileStatement:
                                return VB.Syntax.ExitWhileStatement();
                            case CS.SyntaxKind.SwitchStatement:
                                // If the 'break' is the last statement of a switch block, then we
                                // don't need to translate it into VB (as it is implied).
                                var outerSection = node.FirstAncestorOrSelf<CS.SwitchSectionSyntax>();
                                if (outerSection != null && outerSection.Statements.Count > 0)
                                {
                                    if (node == outerSection.Statements.Last())
                                    {
                                        return VB.Syntax.EmptyStatement();
                                    }
                                }

                                return VB.Syntax.ExitSelectStatement();
                            case CS.SyntaxKind.ForStatement:
                            case CS.SyntaxKind.ForEachStatement:
                                return VB.Syntax.ExitForStatement();
                        }
                    }
                }

                return CreateBadStatement(node, nodeVisitor);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitContinueStatement(CS.ContinueStatementSyntax node)
            {
                return SeparatedList(VisitContinueStatementWorker(node));
            }

            private VB.StatementSyntax VisitContinueStatementWorker(CS.ContinueStatementSyntax node)
            {
                foreach (var parent in node.GetAncestorsOrThis<CS.SyntaxNode>())
                {
                    if (parent.IsContinuableConstruct())
                    {
                        switch (parent.Kind)
                        {
                            case CS.SyntaxKind.DoStatement:
                                return VB.Syntax.ContinueDoStatement();
                            case CS.SyntaxKind.WhileStatement:
                                return VB.Syntax.ContinueWhileStatement();
                            case CS.SyntaxKind.ForStatement:
                            case CS.SyntaxKind.ForEachStatement:
                                return VB.Syntax.ContinueForStatement();
                        }
                    }
                }

                return CreateBadStatement(node, nodeVisitor);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitWhileStatement(CS.WhileStatementSyntax node)
            {
                var begin = VB.Syntax.WhileStatement(
                    condition: nodeVisitor.VisitExpression(node.Condition));

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.WhileBlock(
                        begin,
                        StatementTerminator(),
                        Visit(node.Statement),
                        VB.Syntax.EndWhileStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitForEachStatement(CS.ForEachStatementSyntax node)
            {
                var begin = VB.Syntax.ForEachStatement(
                    controlVariable: VB.Syntax.IdentifierName(nodeVisitor.ConvertIdentifier(node.Identifier)),
                    expression: nodeVisitor.VisitExpression(node.Expression));

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.ForEachBlock(
                        begin,
                        StatementTerminator(),
                        Visit(node.Statement),
                        VB.Syntax.NextStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitYieldStatement(CS.YieldStatementSyntax node)
            {
                // map this to a return statement for now.
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.ReturnStatement(
                        expression: nodeVisitor.VisitExpression(node.Expression)));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitDoStatement(CS.DoStatementSyntax node)
            {
                var begin = VB.Syntax.DoStatement();

                var loop = VB.Syntax.LoopStatement(
                    whileUntilClause: VB.Syntax.WhileClause(condition: nodeVisitor.VisitExpression(node.Condition)));

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.DoLoopBottomTestBlock(
                        begin,
                        StatementTerminator(),
                        Visit(node.Statement),
                        loop));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitUsingStatement(CS.UsingStatementSyntax node)
            {
                VB.UsingStatementSyntax usingStatement;
                if (node.Expression != null)
                {
                    usingStatement = VB.Syntax.UsingStatement().WithExpression(nodeVisitor.VisitExpression(node.Expression));
                }
                else
                {
                    usingStatement = VB.Syntax.UsingStatement().WithVariables(SeparatedCommaList(node.Declaration.Variables.Select(nodeVisitor.Visit<VB.VariableDeclaratorSyntax>)));
                }

                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.UsingBlock(
                        usingStatement,
                        StatementTerminator(),
                        Visit(node.Statement),
                        VB.Syntax.EndUsingStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitLabeledStatement(CS.LabeledStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.LabelStatement(
                        nodeVisitor.ConvertIdentifier(node.Identifier)));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitGotoStatement(CS.GotoStatementSyntax node)
            {
                return SeparatedList(VisitGotoStatementWorker(node));
            }

            private VB.StatementSyntax VisitGotoStatementWorker(CS.GotoStatementSyntax node)
            {
                switch (node.Kind)
                {
                    case CS.SyntaxKind.GotoStatement:
                        return VB.Syntax.GoToStatement(
                            label: VB.Syntax.IdentifierLabel(nodeVisitor.ConvertIdentifier((CS.IdentifierNameSyntax)node.Expression)));
                    case CS.SyntaxKind.GotoDefaultStatement:
                        return VB.Syntax.GoToStatement(
                            label: VB.Syntax.IdentifierLabel(VB.Syntax.Identifier("Else")));
                    case CS.SyntaxKind.GotoCaseStatement:
                        var text = node.Expression.ToString();
                        return VB.Syntax.GoToStatement(
                            label: VB.Syntax.IdentifierLabel(VB.Syntax.Identifier(text)));
                }

                throw new NotImplementedException();
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitEmptyStatement(CS.EmptyStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(VB.Syntax.EmptyStatement(new VB.SyntaxToken()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitLockStatement(CS.LockStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.SyncLockBlock(
                        VB.Syntax.SyncLockStatement(
                            expression: nodeVisitor.VisitExpression(node.Expression)),
                        StatementTerminator(),
                        Visit(node.Statement),
                        VB.Syntax.EndSyncLockStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitTryStatement(CS.TryStatementSyntax node)
            {
                return SeparatedList<VB.StatementSyntax>(
                    VB.Syntax.TryBlock(
                        VB.Syntax.TryPart(
                            VB.Syntax.TryStatement(),
                            StatementTerminator(),
                            Visit(node.Block)),
                        List(node.Catches.Select(nodeVisitor.Visit<VB.CatchPartSyntax>)),
                        nodeVisitor.Visit<VB.FinallyPartSyntax>(node.Finally),
                        VB.Syntax.EndTryStatement()));
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitFixedStatement(CS.FixedStatementSyntax node)
            {
                // todo
                return Visit(node.Statement);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitUnsafeStatement(CS.UnsafeStatementSyntax node)
            {
                return Visit(node.Block);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitCheckedStatement(CS.CheckedStatementSyntax node)
            {
                return Visit(node.Block);
            }

            public override VB.SeparatedSyntaxList<VB.StatementSyntax> DefaultVisit(CS.SyntaxNode node)
            {
                throw new NotImplementedException();
            }
        }
    }
}