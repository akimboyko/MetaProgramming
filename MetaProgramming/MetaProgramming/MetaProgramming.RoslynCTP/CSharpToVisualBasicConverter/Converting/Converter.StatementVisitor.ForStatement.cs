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
using CS = Roslyn.Compilers.CSharp;
using VB = Roslyn.Compilers.VisualBasic;

namespace CSharpToVisualBasicConverter.Converting
{
    public partial class Converter
    {
        private partial class StatementVisitor
        {
            public override VB.SeparatedSyntaxList<VB.StatementSyntax> VisitForStatement(CS.ForStatementSyntax node)
            {
                // VB doesn't have a For statement that directly maps to C#'s.  However, some C# for
                // statements will map to a VB for statement.  Check for those common cases and
                // translate those.
                return IsSimpleForStatement(node)
                    ? VisitSimpleForStatement(node)
                    : VisitComplexForStatement(node);
            }

            private VB.SeparatedSyntaxList<VB.StatementSyntax> VisitSimpleForStatement(CS.ForStatementSyntax node)
            {
                var forStatement = CreateForStatement(node);
                var statements = VisitStatementEnumerable(node.Statement);

                var forBlock = VB.Syntax.ForBlock(
                    forStatement,
                    VB.Syntax.Token(VB.SyntaxKind.StatementTerminatorToken),
                    SeparatedNewLineList(statements),
                    VB.Syntax.NextStatement());

                return VB.Syntax.SeparatedList<VB.StatementSyntax>(forBlock);
            }

            private VB.ForStatementSyntax CreateForStatement(CS.ForStatementSyntax node)
            {
                var variableName = node.Declaration.Variables[0].Identifier.ValueText;
                var stepClause = CreateForStepClause(node);
                var toValue = CreateForToValue(node);
                return VB.Syntax.ForStatement(
                    controlVariable: VB.Syntax.IdentifierName(variableName),
                    fromValue: nodeVisitor.VisitExpression(node.Declaration.Variables[0].Initializer.Value),
                    toValue: toValue,
                    stepClause: stepClause);
            }

            private VB.ExpressionSyntax CreateForToValue(CS.ForStatementSyntax node)
            {
                var expression = nodeVisitor.VisitExpression(((CS.BinaryExpressionSyntax)node.Condition).Right);

                if (node.Condition.Kind != CS.SyntaxKind.LessThanOrEqualExpression &&
                    node.Condition.Kind != CS.SyntaxKind.GreaterThanOrEqualExpression)
                {
                    if (node.Condition.Kind == CS.SyntaxKind.LessThanExpression)
                    {
                        return VB.Syntax.SubtractExpression(
                            expression,
                            right: CreateOneExpression());
                    }

                    if (node.Condition.Kind == CS.SyntaxKind.GreaterThanExpression)
                    {
                        return VB.Syntax.AddExpression(
                            expression,
                            right: CreateOneExpression());
                    }
                }

                return expression;
            }

            private VB.ForStepClauseSyntax CreateForStepClause(CS.ForStatementSyntax node)
            {
                var incrementor = node.Incrementors[0];
                if (incrementor.Kind != CS.SyntaxKind.PreIncrementExpression &&
                    incrementor.Kind != CS.SyntaxKind.PostIncrementExpression)
                {
                    if (incrementor.Kind == CS.SyntaxKind.PreDecrementExpression ||
                        incrementor.Kind == CS.SyntaxKind.PostDecrementExpression)
                    {
                        return VB.Syntax.ForStepClause(
                            stepValue: VB.Syntax.NegateExpression(
                                operand: CreateOneExpression()));
                    }

                    if (incrementor.Kind == CS.SyntaxKind.AddAssignExpression)
                    {
                        return VB.Syntax.ForStepClause(
                            stepValue: nodeVisitor.VisitExpression(((CS.BinaryExpressionSyntax)incrementor).Right));
                    }

                    if (incrementor.Kind == CS.SyntaxKind.SubtractAssignExpression)
                    {
                        return VB.Syntax.ForStepClause(
                            stepValue: VB.Syntax.NegateExpression(
                                operand: nodeVisitor.VisitExpression(((CS.BinaryExpressionSyntax)incrementor).Right)));
                    }
                }

                return null;
            }

            private static VB.LiteralExpressionSyntax CreateOneExpression()
            {
                return VB.Syntax.NumericLiteralExpression(VB.Syntax.IntegerLiteralToken("1", VB.LiteralBase.Decimal, VB.TypeCharacter.None, 1));
            }

            private bool IsSimpleForStatement(CS.ForStatementSyntax node)
            {
                // Has to look like one of the following:
#if false
                for (Declaration; Condition; Incrementor)

                Declaration must be one of:
                var name = v1
                primitive_type name = v1

                Condition must be one of:
                name < v2
                name <= v2
                name > v2
                name >= v2

                Incrementor must be one of:
                name++;
                name--;
                name += v3;
                name -= v3;
#endif
                if (node.Declaration == null ||
                    node.Declaration.Variables.Count != 1)
                {
                    return false;
                }

                var variableName = node.Declaration.Variables[0].Identifier.ValueText;

                return
                    IsSimpleForDeclaration(node) &&
                    IsSimpleForCondition(node, variableName) &&
                    IsSimpleForIncrementor(node, variableName);
            }

            private bool IsSimpleForDeclaration(CS.ForStatementSyntax node)
            {
#if false
                Declaration must be one of:
                var name = v1
                primitive_type name = v1
#endif

                if (node.Declaration != null &&
                    node.Declaration.Variables.Count == 1 &&
                    node.Declaration.Variables[0].Initializer != null)
                {
                    if (node.Declaration.Type.IsVar || node.Declaration.Type.Kind == CS.SyntaxKind.PredefinedType)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool IsSimpleForCondition(CS.ForStatementSyntax node, string variableName)
            {
#if false
                Condition must be one of:
                name < v2
                name <= v2
                name > v2
                name >= v2
#endif
                if (node.Condition != null)
                {
                    if (node.Condition.Kind == CS.SyntaxKind.LessThanExpression ||
                        node.Condition.Kind == CS.SyntaxKind.LessThanOrEqualExpression ||
                        node.Condition.Kind == CS.SyntaxKind.GreaterThanExpression ||
                        node.Condition.Kind == CS.SyntaxKind.GreaterThanOrEqualExpression)
                    {
                        var binaryExpression = (CS.BinaryExpressionSyntax)node.Condition;
                        var identifierName = binaryExpression.Left as CS.IdentifierNameSyntax;
                        return identifierName != null && identifierName.Identifier.ValueText == variableName;
                    }
                }

                return false;
            }

            private bool IsSimpleForIncrementor(CS.ForStatementSyntax node, string variableName)
            {
#if false
                name++;
                name--;
                ++name;
                --name;
                name += v3;
                name -= v3;
#endif
                if (node.Incrementors.Count == 1)
                {
                    var incrementor = node.Incrementors[0];
                    if (incrementor.Kind == CS.SyntaxKind.PostIncrementExpression ||
                        incrementor.Kind == CS.SyntaxKind.PostDecrementExpression)
                    {
                        var identifierName = ((CS.PostfixUnaryExpressionSyntax)incrementor).Operand as CS.IdentifierNameSyntax;
                        return identifierName != null && identifierName.Identifier.ValueText == variableName;
                    }

                    if (incrementor.Kind == CS.SyntaxKind.PreIncrementExpression ||
                        incrementor.Kind == CS.SyntaxKind.PreDecrementExpression)
                    {
                        var identifierName = ((CS.PrefixUnaryExpressionSyntax)incrementor).Operand as CS.IdentifierNameSyntax;
                        return identifierName != null && identifierName.Identifier.ValueText == variableName;
                    }

                    if (incrementor.Kind == CS.SyntaxKind.AddAssignExpression ||
                        incrementor.Kind == CS.SyntaxKind.SubtractAssignExpression)
                    {
                        var binaryExpression = (CS.BinaryExpressionSyntax)incrementor;
                        var identifierName = binaryExpression.Left as CS.IdentifierNameSyntax;
                        return identifierName != null && identifierName.Identifier.ValueText == variableName;
                    }
                }

                return false;
            }

            private VB.SeparatedSyntaxList<VB.StatementSyntax> VisitComplexForStatement(CS.ForStatementSyntax node)
            {
                // VB doesn't have a for loop.  So convert:
                //   for (declarations; condition; incrementors) body into:
                //
                // declarations
                // while (condition) {
                //   body;
                //   incrementors;
                // }

                VB.WhileStatementSyntax begin;
                if (node.Condition == null)
                {
                    begin = VB.Syntax.WhileStatement(
                        condition: VB.Syntax.TrueLiteralExpression(VB.Syntax.Token(VB.SyntaxKind.TrueKeyword)));
                }
                else
                {
                    begin = VB.Syntax.WhileStatement(
                        condition: nodeVisitor.VisitExpression(node.Condition));
                }

                var initialBlock = Visit(node.Statement);

                var whileStatements = initialBlock.Concat(
                    node.Incrementors.Select(nodeVisitor.VisitStatement)).ToList();
                var whileBody = SeparatedNewLineList(whileStatements);

                var whileBlock = VB.Syntax.WhileBlock(
                    begin,
                    StatementTerminator(),
                    whileBody,
                    VB.Syntax.EndWhileStatement());

                var statements = new List<VB.StatementSyntax>();
                if (node.Declaration != null)
                {
                    statements.Add(nodeVisitor.Visit<VB.StatementSyntax>(node.Declaration));
                }

                statements.Add(whileBlock);

                return SeparatedNewLineList(statements);
            }
        }
    }
}
