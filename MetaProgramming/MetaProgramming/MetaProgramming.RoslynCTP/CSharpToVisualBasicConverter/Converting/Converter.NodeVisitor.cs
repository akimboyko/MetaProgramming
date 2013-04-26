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
using Roslyn.Compilers.VisualBasic;
using CS = Roslyn.Compilers.CSharp;
using VB = Roslyn.Compilers.VisualBasic;

namespace CSharpToVisualBasicConverter.Converting
{
    public partial class Converter
    {
        private class NodeVisitor : CS.SyntaxVisitor<VB.SyntaxNode>
        {
            private readonly IText text;
            private readonly IDictionary<string, string> identifierMap;
            private readonly bool convertStrings;
            private readonly StatementVisitor statementVisitor;

            public NodeVisitor(IText text, IDictionary<string, string> identifierMap, bool convertStrings)
            {
                this.text = text;
                this.identifierMap = identifierMap;
                this.convertStrings = convertStrings;
                this.statementVisitor = new StatementVisitor(this, text);
            }

            internal VB.SyntaxToken VisitToken(CS.SyntaxToken token)
            {
                var result = VisitTokenWorker(token);
                if (token.HasLeadingTrivia)
                {
                    result = result.WithLeadingTrivia(ConvertTrivia(token.LeadingTrivia));
                }

                if (token.HasTrailingTrivia)
                {
                    result = result.WithTrailingTrivia(ConvertTrivia(token.TrailingTrivia));
                }

                return result;
            }

            private VB.SyntaxToken VisitTokenWorker(CS.SyntaxToken token)
            {
                var kind = token.Kind;
                if (kind == CS.SyntaxKind.IdentifierToken)
                {
                    return VB.Syntax.Identifier(token.ValueText);
                }

                switch (kind)
                {
                    case CS.SyntaxKind.AbstractKeyword:
                        if (token.Parent is CS.TypeDeclarationSyntax)
                        {
                            return VB.Syntax.Token(VB.SyntaxKind.MustInheritKeyword);
                        }
                        else
                        {
                            return VB.Syntax.Token(VB.SyntaxKind.MustOverrideKeyword);
                        }

                    case CS.SyntaxKind.AssemblyKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.AssemblyKeyword);
                    case CS.SyntaxKind.BoolKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.BooleanKeyword);
                    case CS.SyntaxKind.ByteKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ByteKeyword);
                    case CS.SyntaxKind.ConstKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ConstKeyword);
                    case CS.SyntaxKind.IfKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.IfKeyword);
                    case CS.SyntaxKind.IntKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.IntegerKeyword);
                    case CS.SyntaxKind.InternalKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.FriendKeyword);
                    case CS.SyntaxKind.ModuleKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ModuleKeyword);
                    case CS.SyntaxKind.NewKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.OverloadsKeyword);
                    case CS.SyntaxKind.OutKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ByRefKeyword);
                    case CS.SyntaxKind.OverrideKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.OverridesKeyword);
                    case CS.SyntaxKind.ParamsKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ParamArrayKeyword);
                    case CS.SyntaxKind.PartialKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.PartialKeyword);
                    case CS.SyntaxKind.PrivateKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.PrivateKeyword);
                    case CS.SyntaxKind.ProtectedKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ProtectedKeyword);
                    case CS.SyntaxKind.PublicKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.PublicKeyword);
                    case CS.SyntaxKind.ReadOnlyKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ReadOnlyKeyword);
                    case CS.SyntaxKind.RefKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ByRefKeyword);
                    case CS.SyntaxKind.SealedKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.NotOverridableKeyword);
                    case CS.SyntaxKind.ShortKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ShortKeyword);
                    case CS.SyntaxKind.StaticKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.SharedKeyword);
                    case CS.SyntaxKind.ThisKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.MeKeyword);
                    case CS.SyntaxKind.UIntKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.UIntegerKeyword);
                    case CS.SyntaxKind.UsingKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.ImportsKeyword);
                    case CS.SyntaxKind.VirtualKeyword:
                        return VB.Syntax.Token(VB.SyntaxKind.OverridableKeyword);
                    case CS.SyntaxKind.NumericLiteralToken:
                        return VB.Syntax.IntegerLiteralToken(token.ValueText, VB.LiteralBase.Decimal, VB.TypeCharacter.None, 0);
                    case CS.SyntaxKind.CharacterLiteralToken:
                        {
                            var text = token.ToString().Substring(1, token.ToString().Length - 2);
                            return VB.Syntax.CharacterLiteralToken("\"" + text + "\"c", token.ValueText[0]);
                        }

                    case CS.SyntaxKind.StringLiteralToken:
                        if (CS.SyntaxFacts.IsVerbatimStringLiteral(token))
                        {
                            var text = token.ToString().Substring(2, token.ToString().Length - 3);
                            text = ReplaceNewLines(text);
                            return VB.Syntax.StringLiteralToken("\"" + text + "\"", token.ValueText);
                        }
                        else
                        {
                            var text = token.ToString().Substring(1, token.ToString().Length - 2);
                            text = ReplaceNewLines(text);
                            return VB.Syntax.StringLiteralToken("\"" + text + "\"", token.ValueText);
                        }
                }

                if (CS.SyntaxFacts.IsKeywordKind(kind) ||
                    kind == CS.SyntaxKind.None)
                {
                    return VB.Syntax.Identifier(token.ValueText);
                }
                else if (CS.SyntaxFacts.IsPunctuation(kind))
                {
                    return VB.Syntax.Token(VB.SyntaxKind.EmptyToken);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            internal TSyntaxNode Visit<TSyntaxNode>(CS.SyntaxNode node) where TSyntaxNode : VB.SyntaxNode
            {
                return (TSyntaxNode)Visit(node);
            }

            private VB.TypeSyntax VisitType(CS.TypeSyntax type)
            {
                return Visit<VB.TypeSyntax>(type);
            }

            internal VB.ExpressionSyntax VisitExpression(CS.ExpressionSyntax expression)
            {
                return ConvertToExpression(Visit<VB.SyntaxNode>(expression));
            }

            internal VB.StatementSyntax VisitStatement(CS.ExpressionSyntax expression)
            {
                return ConvertToStatement(Visit<VB.SyntaxNode>(expression));
            }

            private static VB.ExpressionSyntax ConvertToExpression(VB.SyntaxNode node)
            {
                if (node == null)
                {
                    return null;
                }
                else if (node is VB.ExpressionSyntax)
                {
                    return (VB.ExpressionSyntax)node;
                }

                var error = CreateCouldNotBeConvertedString(((CommonSyntaxNode)node).ToFullString(), typeof(VB.ExpressionSyntax));
                return VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(error, error));
            }

            private VB.StatementSyntax ConvertToStatement(VB.SyntaxNode node)
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
                        new VB.SyntaxToken(),
                        (VB.InvocationExpressionSyntax)node);
                }
                else
                {
                    // can happen in error scenarios
                    return CreateBadStatement(((CommonSyntaxNode)node).ToFullString(), typeof(VB.StatementSyntax));
                }
            }

            private VB.SyntaxTriviaList ConvertTrivia(CS.SyntaxTriviaList list)
            {
                return VB.Syntax.TriviaList(list.Where(t => t.Kind != CS.SyntaxKind.DocumentationCommentTrivia)
                                                .SelectMany(VisitTrivia).Aggregate(new List<VB.SyntaxTrivia>(),
                                                    (builder, trivia) => { builder.Add(trivia); return builder; }));
            }

            public override VB.SyntaxNode VisitCompilationUnit(CS.CompilationUnitSyntax node)
            {
                var blocks = List(node.AttributeLists.Select(Visit<VB.AttributeListSyntax>));
                VB.AttributesStatementSyntax attributeStatement = null;
                if (blocks.Count > 0)
                {
                    attributeStatement = VB.Syntax.AttributesStatement(blocks);
                }

                var attributes = SeparatedList(attributeStatement);

                var vbImports = node.Externs.Select(Visit<VB.ImportsStatementSyntax>)
                                    .Concat(node.Usings.Select(Visit<VB.ImportsStatementSyntax>));

                return VB.Syntax.CompilationUnit(
                    SeparatedList<VB.OptionStatementSyntax>(null),
                    SeparatedNewLineList(vbImports),
                    attributes,
                    SeparatedNewLineList(node.Members.Select(Visit<VB.StatementSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.EndOfFileToken));
            }

            private VB.SyntaxList<VB.AttributeListSyntax> ConvertAttributes(
                IEnumerable<CS.AttributeListSyntax> list)
            {
                return List(list.Select(Visit<VB.AttributeListSyntax>));
            }

            public override VB.SyntaxNode VisitUsingDirective(CS.UsingDirectiveSyntax directive)
            {
                if (directive.Alias == null)
                {
                    return VB.Syntax.ImportsStatement(
                        VisitToken(directive.UsingKeyword),
                        SeparatedList<VB.ImportsClauseSyntax>(VB.Syntax.MembersImportsClause(Visit<VB.NameSyntax>(directive.Name))));
                }
                else
                {
                    return VB.Syntax.ImportsStatement(
                        VisitToken(directive.UsingKeyword),
                        SeparatedList<VB.ImportsClauseSyntax>(VB.Syntax.AliasImportsClause(ConvertIdentifier(directive.Alias.Name), VB.Syntax.Token(VB.SyntaxKind.EqualsToken), Visit<VB.NameSyntax>(directive.Name))));
                }
            }

            public override VB.SyntaxNode VisitIdentifierName(CS.IdentifierNameSyntax node)
            {
                return VB.Syntax.IdentifierName(ConvertIdentifier(node));
            }

            internal VB.SyntaxToken ConvertIdentifier(CS.IdentifierNameSyntax name)
            {
                return ConvertIdentifier(name.Identifier);
            }

            internal VB.SyntaxToken ConvertIdentifier(CS.SyntaxToken name)
            {
                var text = name.ValueText;
                string replace;
                if (identifierMap != null && identifierMap.TryGetValue(text, out replace))
                {
                    text = replace;
                }

                if (VB.SyntaxFacts.GetKeywordKind(text) != VB.SyntaxKind.None)
                {
                    return VB.Syntax.Identifier(
                        "[" + text + "]",
                        isBracketed: true,
                        identifierText: text,
                        typeCharacter: VB.TypeCharacter.None);
                }

                return VB.Syntax.Identifier(text);
            }

            internal IEnumerable<VB.SyntaxTrivia> VisitTrivia(CS.SyntaxTrivia trivia)
            {
                if (trivia.HasStructure)
                {
                    var structure = Visit<VB.StructuredTriviaSyntax>(trivia.GetStructure());

                    if (structure is VB.DirectiveTriviaSyntax &&
                        ((VB.DirectiveTriviaSyntax)structure).Directive is VB.BadDirectiveSyntax)
                    {
                        yield return VB.Syntax.CommentTrivia(structure.ToFullString());
                    }
                    else
                    {
                        yield return VB.Syntax.Trivia(structure);
                    }
                }
                else
                {
                    switch (trivia.Kind)
                    {
                        case CS.SyntaxKind.MultiLineCommentTrivia:
                        case CS.SyntaxKind.SingleLineCommentTrivia:
                            yield return VB.Syntax.CommentTrivia("'" + trivia.ToString().Substring(2));
                            break;
                        case CS.SyntaxKind.EndOfLineTrivia:
                        case CS.SyntaxKind.WhitespaceTrivia:
                            yield break;
                        case CS.SyntaxKind.DisabledTextTrivia:
                            yield return VB.Syntax.DisabledTextTrivia(trivia.ToString());
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public override VB.SyntaxNode VisitAliasQualifiedName(CS.AliasQualifiedNameSyntax node)
            {
                // TODO: don't throw away the alias
                return Visit<VB.SyntaxNode>(node.Name);
            }

            public override VB.SyntaxNode VisitQualifiedName(CS.QualifiedNameSyntax node)
            {
                if (node.Right.Kind == CS.SyntaxKind.GenericName)
                {
                    var genericName = (CS.GenericNameSyntax)node.Right;
                    return VB.Syntax.QualifiedName(
                        Visit<VB.NameSyntax>(node.Left),
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.GenericName(ConvertIdentifier(genericName.Identifier),
                        ConvertTypeArguments(genericName.TypeArgumentList)));
                }
                else if (node.Right.Kind == CS.SyntaxKind.IdentifierName)
                {
                    return VB.Syntax.QualifiedName(
                        Visit<VB.NameSyntax>(node.Left),
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.IdentifierName(ConvertIdentifier((CS.IdentifierNameSyntax)node.Right)));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            private VB.TypeArgumentListSyntax ConvertTypeArguments(
                CS.TypeArgumentListSyntax typeArgumentList)
            {
                var types =
                    typeArgumentList.Arguments.Select(Visit<VB.SyntaxNode>).OfType<VB.TypeSyntax>().ToList();
                return VB.Syntax.TypeArgumentList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VB.Syntax.Token(VB.SyntaxKind.OfKeyword),
                    SeparatedCommaList(types),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitTypeParameterList(CS.TypeParameterListSyntax node)
            {
                var parameters = node.Parameters.Select(Visit<VB.TypeParameterSyntax>);

                return VB.Syntax.TypeParameterList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VB.Syntax.Token(VB.SyntaxKind.OfKeyword),
                    SeparatedCommaList(parameters),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            private VB.TypeParameterListSyntax ConvertTypeParameters(CS.SeparatedSyntaxList<CS.TypeParameterSyntax> list)
            {
                var parameters = list.Select(t =>
                {
                    var variance = t.VarianceKeyword.Kind == CS.SyntaxKind.None
                        ? new VB.SyntaxToken()
                        : t.VarianceKeyword.Kind == CS.SyntaxKind.InKeyword
                            ? VB.Syntax.Token(VB.SyntaxKind.InKeyword)
                            : VB.Syntax.Token(VB.SyntaxKind.OutKeyword);

                    // TODO: get the constraints.
                    return VB.Syntax.TypeParameter(
                        variance,
                        ConvertIdentifier(t.Identifier),
                        null);
                }).ToList();

                return VB.Syntax.TypeParameterList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VB.Syntax.Token(VB.SyntaxKind.OfKeyword),
                    SeparatedCommaList(parameters),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitNamespaceDeclaration(CS.NamespaceDeclarationSyntax node)
            {
                return VB.Syntax.NamespaceBlock(
                    VB.Syntax.NamespaceStatement(VB.Syntax.Token(VB.SyntaxKind.NamespaceKeyword), Visit<VB.NameSyntax>(node.Name)),
                    StatementTerminator(),
                    SeparatedNewLineList(node.Members.Select(Visit<VB.StatementSyntax>)),
                    VB.Syntax.EndNamespaceStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.NamespaceKeyword)));
            }

            public override VB.SyntaxNode VisitEnumDeclaration(CS.EnumDeclarationSyntax node)
            {
                VB.SyntaxToken identifier = ConvertIdentifier(node.Identifier);

                var keyword = VB.Syntax.Token(VB.SyntaxKind.EnumKeyword);
                var declaration = VB.Syntax.EnumStatement(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    keyword,
                    identifier,
                    underlyingType: null);

                return VB.Syntax.EnumBlock(
                    declaration,
                    StatementTerminator(),
                    SeparatedNewLineList<VB.StatementSyntax>(node.Members.Select(Visit<VB.EnumMemberDeclarationSyntax>)),
                    VB.Syntax.EndBlockStatement(VB.SyntaxKind.EndEnumStatement, VB.Syntax.Token(VB.SyntaxKind.EndKeyword), keyword));
            }

            public override VB.SyntaxNode VisitClassDeclaration(CS.ClassDeclarationSyntax node)
            {
                VB.SyntaxKind blockKind;
                VB.SyntaxKind declarationKind;
                VB.SyntaxKind endKind;
                VB.SyntaxToken keyword;
                var inherits = SeparatedList<VB.InheritsStatementSyntax>(null);
                var implements = SeparatedList<VB.ImplementsStatementSyntax>(null);

                if (node.Modifiers.Any(CS.SyntaxKind.StaticKeyword))
                {
                    blockKind = VB.SyntaxKind.ModuleBlock;
                    declarationKind = VB.SyntaxKind.ModuleStatement;
                    endKind = VB.SyntaxKind.EndModuleStatement;
                    keyword = VB.Syntax.Token(VB.SyntaxKind.ModuleKeyword);
                }
                else
                {
                    blockKind = VB.SyntaxKind.ClassBlock;
                    declarationKind = VB.SyntaxKind.ClassStatement;
                    endKind = VB.SyntaxKind.EndClassStatement;
                    keyword = VB.Syntax.Token(VB.SyntaxKind.ClassKeyword);
                }

                if (node.BaseList != null && node.BaseList.Types.Count >= 1)
                {
                    // hack. in C# it's just a list of types.  We can't tell if the first one is a
                    // class or not.  So we just check if it starts with a capital I or not and use
                    // that as a weak enough heuristic.
                    var firstType = node.BaseList.Types[0];
                    var rightName = GetRightmostNamePart(firstType);
                    if (rightName.ValueText.Length >= 2 &&
                        rightName.ValueText[0] == 'I' &&
                        char.IsUpper(rightName.ValueText[1]))
                    {
                        implements = ConvertImplementsList(node.BaseList.Types);
                    }
                    else
                    {
                        // first type looks like a class
                        inherits = SeparatedList(
                            VB.Syntax.InheritsStatement(VB.Syntax.Token(VB.SyntaxKind.InheritsKeyword), SeparatedList(VisitType(firstType))));

                        implements = ConvertImplementsList(node.BaseList.Types.Skip(1));
                    }
                }

                return VisitTypeDeclaration(node, blockKind, declarationKind, endKind, keyword, inherits, implements);
            }

            public override VB.SyntaxNode VisitStructDeclaration(CS.StructDeclarationSyntax node)
            {
                var blockKind = VB.SyntaxKind.StructureBlock;
                var declarationKind = VB.SyntaxKind.StructureStatement;
                var endKind = VB.SyntaxKind.EndStructureStatement;
                var keyword = VB.Syntax.Token(VB.SyntaxKind.StructureKeyword);
                var implements = SeparatedList<VB.ImplementsStatementSyntax>(null);
                if (node.BaseList != null)
                {
                    implements = ConvertImplementsList(node.BaseList.Types);
                }

                return VisitTypeDeclaration(node, blockKind, declarationKind, endKind, keyword, inherits: SeparatedList<VB.InheritsStatementSyntax>(null), implements: implements);
            }

            public override VB.SyntaxNode VisitInterfaceDeclaration(CS.InterfaceDeclarationSyntax node)
            {
                var blockKind = VB.SyntaxKind.InterfaceBlock;
                var declarationKind = VB.SyntaxKind.InterfaceStatement;
                var endKind = VB.SyntaxKind.EndInterfaceStatement;
                var keyword = VB.Syntax.Token(VB.SyntaxKind.InterfaceKeyword);
                var inherits = SeparatedList<VB.InheritsStatementSyntax>(null);
                if (node.BaseList != null)
                {
                    inherits = ConvertInheritsList(node.BaseList.Types);
                }

                return VisitTypeDeclaration(node, blockKind, declarationKind, endKind, keyword, inherits: inherits, implements: SeparatedList<VB.ImplementsStatementSyntax>(null));
            }

            private VB.SyntaxNode VisitTypeDeclaration(CS.TypeDeclarationSyntax node, VB.SyntaxKind blockKind, VB.SyntaxKind declarationKind, VB.SyntaxKind endKind,
                VB.SyntaxToken keyword, VB.SeparatedSyntaxList<VB.InheritsStatementSyntax> inherits, VB.SeparatedSyntaxList<VB.ImplementsStatementSyntax> implements)
            {
                VB.SyntaxToken identifier = ConvertIdentifier(node.Identifier);
                VB.TypeParameterListSyntax typeParameters = Visit<VB.TypeParameterListSyntax>(node.TypeParameterList);

                var declaration = VB.Syntax.TypeStatement(
                    declarationKind,
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers.Where(t => t.Kind != CS.SyntaxKind.StaticKeyword)),
                    keyword,
                    identifier,
                    typeParameters);

                var typeBlock = VB.Syntax.TypeBlock(
                    blockKind,
                    declaration,
                    StatementTerminator(),
                    inherits,
                    implements,
                    SeparatedNewLineList(node.Members.Select(Visit<VB.StatementSyntax>)),
                    VB.Syntax.EndBlockStatement(endKind, VB.Syntax.Token(VB.SyntaxKind.EndKeyword), keyword));

                var docComment = node.GetLeadingTrivia().FirstOrDefault(t => t.Kind == CS.SyntaxKind.DocumentationCommentTrivia);
                if (docComment.Kind != CS.SyntaxKind.None)
                {
                    var vbDocComment = VisitTrivia(docComment);
                    return typeBlock.WithLeadingTrivia(typeBlock.GetLeadingTrivia().Concat(vbDocComment));
                }
                else
                {
                    return typeBlock;
                }
            }

            private CS.SyntaxToken GetRightmostNamePart(CS.TypeSyntax type)
            {
                while (true)
                {
                    if (type.Kind == CS.SyntaxKind.IdentifierName)
                    {
                        return ((CS.IdentifierNameSyntax)type).Identifier;
                    }
                    else if (type.Kind == CS.SyntaxKind.QualifiedName)
                    {
                        type = ((CS.QualifiedNameSyntax)type).Right;
                    }
                    else if (type.Kind == CS.SyntaxKind.GenericName)
                    {
                        return ((CS.GenericNameSyntax)type).Identifier;
                    }
                    else if (type.Kind == CS.SyntaxKind.AliasQualifiedName)
                    {
                        type = ((CS.AliasQualifiedNameSyntax)type).Name;
                    }
                    else
                    {
                        System.Diagnostics.Debug.Fail("Unexpected type syntax kind.");
                        return default(CS.SyntaxToken);
                    }
                }
            }

            private VB.SeparatedSyntaxList<VB.ImplementsStatementSyntax> ConvertImplementsList(IEnumerable<CS.TypeSyntax> types)
            {
                var vbTypes = types.Select(t =>
                    VB.Syntax.ImplementsStatement(
                        VB.Syntax.Token(VB.SyntaxKind.ImplementsKeyword),
                        SeparatedList(VisitType(t))));
                return SeparatedNewLineList(vbTypes);
            }

            private VB.SeparatedSyntaxList<VB.InheritsStatementSyntax> ConvertInheritsList(IEnumerable<CS.TypeSyntax> types)
            {
                var vbTypes = types.Select(t =>
                    VB.Syntax.InheritsStatement(
                        VB.Syntax.Token(VB.SyntaxKind.InheritsKeyword),
                        SeparatedList(VisitType(t))));
                return SeparatedNewLineList(vbTypes);
            }

            private VB.SyntaxTokenList TokenList(IEnumerable<VB.SyntaxToken> tokens)
            {
                return VB.Syntax.TokenList(tokens.Aggregate(new List<VB.SyntaxToken>(), (builder, token) => { builder.Add(token); return builder; }));
            }

            internal VB.SyntaxTokenList ConvertModifiers(IEnumerable<CS.SyntaxToken> list)
            {
                return TokenList(list.Where(t => t.Kind != CS.SyntaxKind.ThisKeyword).Select(VisitToken));
            }

            public override VB.SyntaxNode VisitMethodDeclaration(CS.MethodDeclarationSyntax node)
            {
                var isVoid =
                    node.ReturnType.Kind == CS.SyntaxKind.PredefinedType &&
                        ((CS.PredefinedTypeSyntax)node.ReturnType).Keyword.Kind == CS.SyntaxKind.VoidKeyword;

                VB.ImplementsClauseSyntax implementsClause = null;
                if (node.ExplicitInterfaceSpecifier != null)
                {
                    implementsClause = VB.Syntax.ImplementsClause(
                        VB.Syntax.Token(VB.SyntaxKind.ImplementsKeyword),
                        VB.Syntax.SeparatedList(
                            VB.Syntax.QualifiedName(
                                (VB.NameSyntax)VisitType(node.ExplicitInterfaceSpecifier.Name),
                                VB.Syntax.Token(VB.SyntaxKind.DotToken),
                                VB.Syntax.IdentifierName(VisitToken(node.Identifier)))));
                }

                VB.MethodStatementSyntax begin;

                VB.SyntaxToken identifier = ConvertIdentifier(node.Identifier);
                VB.TypeParameterListSyntax typeParameters = Visit<VB.TypeParameterListSyntax>(node.TypeParameterList);

                var isExtension =
                    node.ParameterList.Parameters.Count > 0 &&
                    node.ParameterList.Parameters[0].Modifiers.Any(CS.SyntaxKind.ThisKeyword);

                var modifiers = isExtension
                     ? node.Modifiers.Where(t => t.Kind != CS.SyntaxKind.StaticKeyword).ToList()
                     : node.Modifiers.ToList();

                var attributes = isExtension
                    ? node.AttributeLists.Concat(CreateExtensionAttribute()).ToList()
                    : node.AttributeLists.ToList();

                if (isVoid)
                {
                    begin = VB.Syntax.SubStatement(
                        ConvertAttributes(attributes),
                        ConvertModifiers(modifiers),
                        VB.Syntax.Token(VB.SyntaxKind.SubKeyword),
                        identifier,
                        typeParameters,
                        Visit<VB.ParameterListSyntax>(node.ParameterList),
                        asClause: null,
                        handlesClause: null,
                        implementsClause: implementsClause);
                }
                else
                {
                    VB.SyntaxList<VB.AttributeListSyntax> returnAttributes;
                    VB.SyntaxList<VB.AttributeListSyntax> remainAttributes;
                    SplitAttributes(attributes, out returnAttributes, out remainAttributes);

                    begin = VB.Syntax.FunctionStatement(
                        remainAttributes,
                        ConvertModifiers(modifiers),
                        VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword),
                        identifier,
                        typeParameters,
                        Visit<VB.ParameterListSyntax>(node.ParameterList),
                        VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), returnAttributes, VisitType(node.ReturnType)),
                        handlesClause: null,
                        implementsClause: implementsClause);
                }

                var docComment = node.GetLeadingTrivia().FirstOrDefault(t => t.Kind == CS.SyntaxKind.DocumentationCommentTrivia);
                if (docComment.Kind != CS.SyntaxKind.None)
                {
                    var vbDocComment = VisitTrivia(docComment);
                    begin = begin.WithLeadingTrivia(begin.GetLeadingTrivia().Concat(vbDocComment));
                }

                if (node.Body == null)
                {
                    return begin;
                }

                if (isVoid)
                {
                    return VB.Syntax.SubBlock(
                        begin,
                        StatementTerminator(),
                        statementVisitor.VisitStatement(node.Body),
                        VB.Syntax.EndSubStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.SubKeyword)));
                }
                else
                {
                    return VB.Syntax.FunctionBlock(
                        begin,
                        StatementTerminator(),
                        statementVisitor.VisitStatement(node.Body),
                        VB.Syntax.EndFunctionStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword)));
                }
            }

            private CS.AttributeListSyntax CreateExtensionAttribute()
            {
                return CS.Syntax.AttributeList(
                    attributes: CS.Syntax.SeparatedList(
                        CS.Syntax.Attribute(CS.Syntax.ParseName("System.Runtime.CompilerServices.Extension"))));
            }

            private void SplitAttributes(IList<CS.AttributeListSyntax> attributes, out VB.SyntaxList<VB.AttributeListSyntax> returnAttributes, out VB.SyntaxList<VB.AttributeListSyntax> remainingAttributes)
            {
                var returnAttribute =
                    attributes.FirstOrDefault(a => a.Target != null && a.Target.Identifier.Kind == CS.SyntaxKind.ReturnKeyword);

                var rest =
                    attributes.Where(a => a != returnAttribute);

                returnAttributes = List(Visit<VB.AttributeListSyntax>(returnAttribute));
                remainingAttributes = ConvertAttributes(rest);
            }

            public override VB.SyntaxNode VisitParameterList(CS.ParameterListSyntax node)
            {
                return VB.Syntax.ParameterList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    SeparatedCommaList(node.Parameters.Select(Visit<VB.ParameterSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitBracketedParameterList(CS.BracketedParameterListSyntax node)
            {
                return VB.Syntax.ParameterList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    SeparatedCommaList(node.Parameters.Select(Visit<VB.ParameterSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitParameter(CS.ParameterSyntax node)
            {
                var asClause = node.Type == null
                    ? null
                    : VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type));
                var modifiers = ConvertModifiers(node.Modifiers);
                if (node.Default != null)
                {
                    modifiers = TokenList(modifiers.Concat(VB.Syntax.Token(VB.SyntaxKind.OptionalKeyword)));
                }

                return VB.Syntax.Parameter(
                    ConvertAttributes(node.AttributeLists),
                    modifiers,
                    VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()),
                    asClause,
                    node.Default == null ? null : VB.Syntax.EqualsValue(VB.Syntax.Token(VB.SyntaxKind.EqualsToken), VisitExpression(node.Default.Value)));
            }

            public override VB.SyntaxNode VisitGenericName(CS.GenericNameSyntax node)
            {
                return VB.Syntax.GenericName(
                    ConvertIdentifier(node.Identifier),
                    ConvertTypeArguments(node.TypeArgumentList));
            }

            public override VB.SyntaxNode VisitTypeParameter(CS.TypeParameterSyntax node)
            {
                var variance = node.VarianceKeyword.Kind == CS.SyntaxKind.None
                    ? default(VB.SyntaxToken)
                    : node.VarianceKeyword.Kind == CS.SyntaxKind.InKeyword
                        ? VB.Syntax.Token(VB.SyntaxKind.InKeyword)
                        : VB.Syntax.Token(VB.SyntaxKind.OutKeyword);

                // TODO: get the constraints.
                return VB.Syntax.TypeParameter(
                    variance,
                    ConvertIdentifier(node.Identifier),
                    null);
            }

            public override VB.SyntaxNode VisitPredefinedType(CS.PredefinedTypeSyntax node)
            {
                switch (node.Keyword.Kind)
                {
                    case CS.SyntaxKind.BoolKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.BooleanKeyword));
                    case CS.SyntaxKind.ByteKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.ByteKeyword));
                    case CS.SyntaxKind.CharKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.CharKeyword));
                    case CS.SyntaxKind.DecimalKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.DecimalKeyword));
                    case CS.SyntaxKind.DoubleKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.DoubleKeyword));
                    case CS.SyntaxKind.FloatKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.SingleKeyword));
                    case CS.SyntaxKind.IntKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.IntegerKeyword));
                    case CS.SyntaxKind.LongKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.LongKeyword));
                    case CS.SyntaxKind.ObjectKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.ObjectKeyword));
                    case CS.SyntaxKind.SByteKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.SByteKeyword));
                    case CS.SyntaxKind.ShortKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.ShortKeyword));
                    case CS.SyntaxKind.StringKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.StringKeyword));
                    case CS.SyntaxKind.UIntKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.UIntegerKeyword));
                    case CS.SyntaxKind.ULongKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.ULongKeyword));
                    case CS.SyntaxKind.UShortKeyword:
                        return VB.Syntax.PredefinedType(VB.Syntax.Token(VB.SyntaxKind.UShortKeyword));
                    case CS.SyntaxKind.VoidKeyword:
                        return VB.Syntax.IdentifierName(VB.Syntax.Identifier("Void"));
                    default:
                        throw new NotImplementedException();
                }
            }

            public override VB.SyntaxNode VisitBaseExpression(CS.BaseExpressionSyntax node)
            {
                return VB.Syntax.MyBaseExpression(VB.Syntax.Token(VB.SyntaxKind.MyBaseKeyword));
            }

            public override VB.SyntaxNode VisitThisExpression(CS.ThisExpressionSyntax node)
            {
                return VB.Syntax.MeExpression(VB.Syntax.Token(VB.SyntaxKind.MeKeyword));
            }

            public override VB.SyntaxNode VisitLiteralExpression(CS.LiteralExpressionSyntax node)
            {
                switch (node.Kind)
                {
                    case CS.SyntaxKind.ArgListExpression:
                        var error = CreateCouldNotBeConvertedString(node.ToFullString(), typeof(VB.SyntaxNode));
                        return VB.Syntax.StringLiteralExpression(
                            VB.Syntax.StringLiteralToken(error, error));
                    case CS.SyntaxKind.BaseExpression:
                        return VB.Syntax.MyBaseExpression(VB.Syntax.Token(VB.SyntaxKind.MyBaseKeyword));
                    case CS.SyntaxKind.NumericLiteralExpression:
                    case CS.SyntaxKind.StringLiteralExpression:
                    case CS.SyntaxKind.CharacterLiteralExpression:
                    case CS.SyntaxKind.TrueLiteralExpression:
                    case CS.SyntaxKind.FalseLiteralExpression:
                    case CS.SyntaxKind.NullLiteralExpression:
                        return ConvertLiteralExpression(node);
                }

                throw new NotImplementedException();
            }

            private VB.SyntaxNode ConvertLiteralExpression(CS.LiteralExpressionSyntax node)
            {
                switch (node.Token.Kind)
                {
                    case CS.SyntaxKind.CharacterLiteralToken:
                        return VB.Syntax.CharacterLiteralExpression(
                            VB.Syntax.CharacterLiteralToken("\"" + node.Token.ToString().Substring(1, Math.Max(node.Token.ToString().Length - 2, 0)) + "\"c", (char)node.Token.Value));
                    case CS.SyntaxKind.FalseKeyword:
                        return VB.Syntax.FalseLiteralExpression(VB.Syntax.Token(VB.SyntaxKind.FalseKeyword));
                    case CS.SyntaxKind.NullKeyword:
                        return VB.Syntax.NothingLiteralExpression(VB.Syntax.Token(VB.SyntaxKind.NothingKeyword));
                    case CS.SyntaxKind.NumericLiteralToken:
                        return ConvertNumericLiteralToken(node);
                    case CS.SyntaxKind.StringLiteralToken:
                        return ConvertStringLiteralExpression(node);
                    case CS.SyntaxKind.TrueKeyword:
                        return VB.Syntax.TrueLiteralExpression(VB.Syntax.Token(VB.SyntaxKind.TrueKeyword));
                }

                throw new NotImplementedException();
            }

            private VB.SyntaxNode ConvertNumericLiteralToken(CS.LiteralExpressionSyntax node)
            {
                if (node.Token.ToString().StartsWith("0x") ||
                    node.Token.ToString().StartsWith("0X"))
                {
                    return VB.Syntax.NumericLiteralExpression(
                        VB.Syntax.IntegerLiteralToken(
                            "&H" + node.Token.ToString().Substring(2).ToUpperInvariant(),
                            VB.LiteralBase.Hexadecimal,
                            VB.TypeCharacter.None,
                            0));
                }

                // TODO: handle the other numeric types.
                return VB.Syntax.NumericLiteralExpression(
                    VB.Syntax.IntegerLiteralToken(node.Token.ToString(), VB.LiteralBase.Decimal, VB.TypeCharacter.None, 0));
            }

            private VB.SyntaxNode ConvertStringLiteralExpression(CS.LiteralExpressionSyntax node)
            {
                var start = this.text.GetLineNumberFromPosition(node.Token.Span.Start);
                var end = this.text.GetLineNumberFromPosition(node.Token.Span.End);

                if (CS.SyntaxFacts.IsVerbatimStringLiteral(node.Token) &&
                    start != end)
                {
                    var text = node.Token.ToString();
                    text = text.Substring(2, text.Length - 3);
                    text = System.Security.SecurityElement.Escape(text);

                    return VB.Syntax.MemberAccessExpression(
                        VB.Syntax.XmlElement(
                            VB.Syntax.XmlElementStartTag(
                                VB.Syntax.Token(VB.SyntaxKind.LessThanToken),
                                VB.Syntax.XmlName(null, VB.Syntax.XmlNameToken("text", VB.SyntaxKind.XmlNameToken)),
                                VB.Syntax.List<VB.XmlNodeSyntax>(),
                                VB.Syntax.Token(VB.SyntaxKind.GreaterThanToken)),
                            List<VB.XmlNodeSyntax>(VB.Syntax.XmlText(VB.Syntax.TokenList(VB.Syntax.XmlTextLiteralToken(text, text)))),
                            VB.Syntax.XmlElementEndTag(
                                VB.Syntax.Token(VB.SyntaxKind.LessThanSlashToken),
                                VB.Syntax.XmlName(null, VB.Syntax.XmlNameToken("text", VB.SyntaxKind.XmlNameToken)),
                                VB.Syntax.Token(VB.SyntaxKind.GreaterThanToken))),
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.IdentifierName(VB.Syntax.Identifier("Value")));
                }
                else
                {
                    return VB.Syntax.StringLiteralExpression(VisitToken(node.Token));
                }
            }

            public override VB.SyntaxNode VisitVariableDeclarator(CS.VariableDeclaratorSyntax node)
            {
                var type = node.GetVariableType();
                var isVar = type is CS.IdentifierNameSyntax && ((CS.IdentifierNameSyntax)type).Identifier.ValueText == "var";

                VB.EqualsValueSyntax initializer = null;
                if (node.Initializer != null)
                {
                    initializer = VB.Syntax.EqualsValue(
                        VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                        VisitExpression(node.Initializer.Value));
                }

                return VB.Syntax.VariableDeclarator(
                    SeparatedList(VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>())),
                    isVar ? null : VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(type)),
                    initializer);
            }

            public override VB.SyntaxNode VisitObjectCreationExpression(CS.ObjectCreationExpressionSyntax node)
            {
                return VB.Syntax.ObjectCreationExpression(
                    VB.Syntax.Token(VB.SyntaxKind.NewKeyword),
                    null,
                    VisitType(node.Type),
                    Visit<VB.ArgumentListSyntax>(node.ArgumentList),
                    Visit<VB.ObjectCreationInitializerSyntax>(node.Initializer));
            }

            public override VB.SyntaxNode VisitArgumentList(CS.ArgumentListSyntax node)
            {
                return VB.Syntax.ArgumentList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    SeparatedCommaList(node.Arguments.Select(Visit<VB.ArgumentSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitBracketedArgumentList(CS.BracketedArgumentListSyntax node)
            {
                return VB.Syntax.ArgumentList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    SeparatedCommaList(node.Arguments.Select(Visit<VB.ArgumentSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitArgument(CS.ArgumentSyntax node)
            {
                if (node.NameColon == null)
                {
                    return VB.Syntax.SimpleArgument(VisitExpression(node.Expression));
                }
                else
                {
                    return VB.Syntax.NamedArgument(
                        VB.Syntax.IdentifierName(ConvertIdentifier(node.NameColon.Name)),
                        VB.Syntax.Token(VB.SyntaxKind.ColonEqualsToken),
                        VisitExpression(node.Expression));
                }
            }

            public override VB.SyntaxNode VisitInvocationExpression(CS.InvocationExpressionSyntax node)
            {
                return VB.Syntax.InvocationExpression(
                    VisitExpression(node.Expression),
                    Visit<VB.ArgumentListSyntax>(node.ArgumentList));
            }

            public override VB.SyntaxNode VisitFieldDeclaration(CS.FieldDeclarationSyntax node)
            {
                var modifiers = ConvertModifiers(node.Modifiers);
                if (modifiers.Count == 0)
                {
                    modifiers = VB.Syntax.TokenList(VB.Syntax.Token(VB.SyntaxKind.DimKeyword));
                }

                return VB.Syntax.FieldDeclaration(
                    ConvertAttributes(node.AttributeLists),
                    modifiers,
                    SeparatedCommaList(node.Declaration.Variables.Select(Visit<VB.VariableDeclaratorSyntax>)));
            }

            public override VB.SyntaxNode VisitConstructorDeclaration(CS.ConstructorDeclarationSyntax node)
            {
                var declaration = VB.Syntax.ConstructorStatement(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    VB.Syntax.Token(VB.SyntaxKind.SubKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.NewKeyword),
                    Visit<VB.ParameterListSyntax>(node.ParameterList),
                    asClause: null);

                if (node.Body == null)
                {
                    return declaration;
                }

                return VB.Syntax.ConstructorBlock(
                    declaration,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Body),
                    VB.Syntax.EndSubStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.SubKeyword)));
            }

            public override VB.SyntaxNode VisitMemberAccessExpression(CS.MemberAccessExpressionSyntax node)
            {
                if (node.Name.Kind == CS.SyntaxKind.IdentifierName)
                {
                    return VB.Syntax.MemberAccessExpression(
                        VisitExpression(node.Expression),
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.IdentifierName(ConvertIdentifier((CS.IdentifierNameSyntax)node.Name)));
                }
                else if (node.Name.Kind == CS.SyntaxKind.GenericName)
                {
                    var genericName = (CS.GenericNameSyntax)node.Name;
                    var memberAccess = VB.Syntax.MemberAccessExpression(
                        VisitExpression(node.Expression),
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.GenericName(ConvertIdentifier(genericName.Identifier),
                        ConvertTypeArguments(genericName.TypeArgumentList)));
                    return memberAccess;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            public override VB.SyntaxNode VisitBinaryExpression(CS.BinaryExpressionSyntax node)
            {
                var left = VisitExpression(node.Left);
                var right = VisitExpression(node.Right);

                switch (node.OperatorToken.Kind)
                {
                    case CS.SyntaxKind.AmpersandAmpersandToken:
                        return VB.Syntax.AndAlsoExpression(left, VB.Syntax.Token(VB.SyntaxKind.AndAlsoKeyword), right);
                    case CS.SyntaxKind.AmpersandToken:
                        return VB.Syntax.AndExpression(left, VB.Syntax.Token(VB.SyntaxKind.AndKeyword), right);
                    case CS.SyntaxKind.AmpersandEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.AndAlsoExpression(left2, VB.Syntax.Token(VB.SyntaxKind.AndAlsoKeyword), right));
                        }

                    case CS.SyntaxKind.AsKeyword:
                        return VB.Syntax.TryCastExpression(
                            VB.Syntax.Token(VB.SyntaxKind.TryCastKeyword),
                            VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                            left,
                            VB.Syntax.Token(VB.SyntaxKind.CommaToken),
                            (VB.TypeSyntax)right,
                            VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
                    case CS.SyntaxKind.AsteriskToken:
                        return VB.Syntax.MultiplyExpression(left, VB.Syntax.Token(VB.SyntaxKind.AsteriskToken), right);
                    case CS.SyntaxKind.AsteriskEqualsToken:
                        return VB.Syntax.MultiplyAssignment(left, VB.Syntax.Token(VB.SyntaxKind.AsteriskEqualsToken), right);
                    case CS.SyntaxKind.BarToken:
                        return VB.Syntax.OrExpression(left, VB.Syntax.Token(VB.SyntaxKind.OrKeyword), right);
                    case CS.SyntaxKind.BarBarToken:
                        return VB.Syntax.OrElseExpression(left, VB.Syntax.Token(VB.SyntaxKind.OrElseKeyword), right);
                    case CS.SyntaxKind.BarEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.OrElseExpression(left2, VB.Syntax.Token(VB.SyntaxKind.OrElseKeyword), right));
                        }

                    case CS.SyntaxKind.CaretToken:
                        return VB.Syntax.XorExpression(left, VB.Syntax.Token(VB.SyntaxKind.XorKeyword), right);
                    case CS.SyntaxKind.CaretEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.XorExpression(left2, VB.Syntax.Token(VB.SyntaxKind.XorKeyword), right));
                        }

                    case CS.SyntaxKind.MinusToken:
                        return VB.Syntax.SubtractExpression(left, VB.Syntax.Token(VB.SyntaxKind.MinusToken), right);
                    case CS.SyntaxKind.MinusEqualsToken:
                        return VB.Syntax.SubtractAssignment(left, VB.Syntax.Token(VB.SyntaxKind.MinusEqualsToken), right);

                    case CS.SyntaxKind.EqualsEqualsToken:
                        if (node.Right.Kind == CS.SyntaxKind.NullLiteralExpression)
                        {
                            return VB.Syntax.IsExpression(left, VB.Syntax.Token(VB.SyntaxKind.IsKeyword), right);
                        }
                        else
                        {
                            return VB.Syntax.EqualsExpression(left, VB.Syntax.Token(VB.SyntaxKind.EqualsToken), right);
                        }

                    case CS.SyntaxKind.EqualsToken:
                        return VB.Syntax.AssignmentStatement(left, VB.Syntax.Token(VB.SyntaxKind.EqualsToken), right);
                    case CS.SyntaxKind.ExclamationEqualsToken:
                        if (node.Right.Kind == CS.SyntaxKind.NullLiteralExpression)
                        {
                            return VB.Syntax.IsNotExpression(left, VB.Syntax.Token(VB.SyntaxKind.IsNotKeyword), right);
                        }
                        else
                        {
                            return VB.Syntax.NotEqualsExpression(left, VB.Syntax.Token(VB.SyntaxKind.LessThanGreaterThanToken), right);
                        }

                    case CS.SyntaxKind.GreaterThanToken:
                        return VB.Syntax.GreaterThanExpression(left, VB.Syntax.Token(VB.SyntaxKind.GreaterThanToken), right);
                    case CS.SyntaxKind.GreaterThanEqualsToken:
                        return VB.Syntax.GreaterThanOrEqualExpression(left, VB.Syntax.Token(VB.SyntaxKind.GreaterThanEqualsToken), right);
                    case CS.SyntaxKind.GreaterThanGreaterThanToken:
                        return VB.Syntax.RightShiftExpression(left, VB.Syntax.Token(VB.SyntaxKind.GreaterThanGreaterThanToken), right);
                    case CS.SyntaxKind.GreaterThanGreaterThanEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.RightShiftExpression(left2, VB.Syntax.Token(VB.SyntaxKind.GreaterThanGreaterThanToken), right));
                        }

                    case CS.SyntaxKind.IsKeyword:
                        return VB.Syntax.TypeOfIsExpression(VB.Syntax.Token(VB.SyntaxKind.TypeOfKeyword), left, VB.Syntax.Token(VB.SyntaxKind.IsKeyword), (VB.TypeSyntax)right);
                    case CS.SyntaxKind.LessThanLessThanEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.LeftShiftExpression(left2, VB.Syntax.Token(VB.SyntaxKind.LessThanLessThanToken), right));
                        }

                    case CS.SyntaxKind.LessThanToken:
                        return VB.Syntax.LessThanExpression(left, VB.Syntax.Token(VB.SyntaxKind.LessThanToken), right);
                    case CS.SyntaxKind.LessThanEqualsToken:
                        return VB.Syntax.LessThanOrEqualExpression(left, VB.Syntax.Token(VB.SyntaxKind.LessThanEqualsToken), right);
                    case CS.SyntaxKind.LessThanLessThanToken:
                        return VB.Syntax.LeftShiftExpression(left, VB.Syntax.Token(VB.SyntaxKind.LessThanLessThanToken), right);
                    case CS.SyntaxKind.PercentToken:
                        return VB.Syntax.ModuloExpression(left, VB.Syntax.Token(VB.SyntaxKind.ModKeyword), right);
                    case CS.SyntaxKind.PercentEqualsToken:
                        {
                            var left2 = VisitExpression(node.Left);
                            return VB.Syntax.AssignmentStatement(
                                left,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.ModuloExpression(left2, VB.Syntax.Token(VB.SyntaxKind.ModKeyword), right));
                        }

                    case CS.SyntaxKind.PlusToken:
                        return VB.Syntax.AddExpression(left, VB.Syntax.Token(VB.SyntaxKind.PlusToken), right);
                    case CS.SyntaxKind.PlusEqualsToken:
                        return VB.Syntax.AddAssignment(left, VB.Syntax.Token(VB.SyntaxKind.PlusEqualsToken), right);
                    case CS.SyntaxKind.QuestionToken:
                    case CS.SyntaxKind.QuestionQuestionToken:
                        {
                            var args = new VB.ArgumentSyntax[] { VB.Syntax.SimpleArgument(left), VB.Syntax.SimpleArgument(right) };
                            var arguments = SeparatedCommaList(args);
                            return VB.Syntax.InvocationExpression(
                                VB.Syntax.IdentifierName(VB.Syntax.Identifier("If")),
                                VB.Syntax.ArgumentList(VB.Syntax.Token(VB.SyntaxKind.OpenParenToken), arguments, VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
                        }

                    case CS.SyntaxKind.SlashToken:
                        return VB.Syntax.DivideExpression(left, VB.Syntax.Token(VB.SyntaxKind.SlashToken), right);

                    case CS.SyntaxKind.SlashEqualsToken:
                        return VB.Syntax.DivideAssignment(left, VB.Syntax.Token(VB.SyntaxKind.SlashEqualsToken), right);
                }

                throw new NotImplementedException();
            }

            public override VB.SyntaxNode VisitElseClause(CS.ElseClauseSyntax node)
            {
                var begin = VB.Syntax.ElseStatement(VB.Syntax.Token(VB.SyntaxKind.ElseKeyword));

                return VB.Syntax.ElsePart(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Statement));
            }

            public override VB.SyntaxNode VisitSwitchSection(CS.SwitchSectionSyntax node)
            {
                return VB.Syntax.CaseBlock(
                    VB.Syntax.CaseStatement(
                        VB.Syntax.Token(VB.SyntaxKind.CaseKeyword),
                        SeparatedCommaList(node.Labels.Select(Visit<VB.CaseClauseSyntax>))),
                    StatementTerminator(),
                    SeparatedNewLineList(
                        node.Statements.SelectMany(statementVisitor.VisitStatementEnumerable).ToList()));
            }

            public override VB.SyntaxNode VisitSwitchLabel(CS.SwitchLabelSyntax node)
            {
                if (node.Kind == CS.SyntaxKind.DefaultSwitchLabel)
                {
                    return VB.Syntax.CaseElseClause(VB.Syntax.Token(VB.SyntaxKind.ElseKeyword));
                }
                else
                {
                    return VB.Syntax.CaseValueClause(VisitExpression(node.Value));
                }
            }

            public override VB.SyntaxNode VisitCastExpression(CS.CastExpressionSyntax node)
            {
                // todo: need to handle CInt and all that junk.
                return VB.Syntax.DirectCastExpression(
                    VB.Syntax.Token(VB.SyntaxKind.DirectCastKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VisitExpression(node.Expression),
                    VB.Syntax.Token(VB.SyntaxKind.CommaToken),
                    VisitType(node.Type),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitParenthesizedLambdaExpression(CS.ParenthesizedLambdaExpressionSyntax node)
            {
                var parameters = Visit<VB.ParameterListSyntax>(node.ParameterList);
                var lambdaHeader = VB.Syntax.FunctionLambdaHeader(
                    new VB.SyntaxList<VB.AttributeListSyntax>(),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword),
                    parameters,
                    null);
                if (node.Body.Kind == CS.SyntaxKind.Block)
                {
                    return VB.Syntax.MultiLineFunctionLambdaExpression(
                        lambdaHeader,
                        StatementTerminator(),
                        statementVisitor.VisitStatement((CS.BlockSyntax)node.Body),
                        VB.Syntax.EndFunctionStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword)));
                }
                else
                {
                    return VB.Syntax.SingleLineFunctionLambdaExpression(
                        lambdaHeader,
                        VB.Syntax.Token(VB.SyntaxKind.StatementTerminatorToken, string.Empty),
                        Visit<VB.SyntaxNode>(node.Body));
                }
            }

            public override VB.SyntaxNode VisitSimpleLambdaExpression(CS.SimpleLambdaExpressionSyntax node)
            {
                var parameter = VB.Syntax.Parameter(
                    new VB.SyntaxList<VB.AttributeListSyntax>(),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.ModifiedIdentifier(
                        ConvertIdentifier(node.Parameter.Identifier),
                        new VB.SyntaxToken(),
                        null,
                        new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()),
                    null, null);
                var lambdaHeader = VB.Syntax.FunctionLambdaHeader(
                    new VB.SyntaxList<VB.AttributeListSyntax>(),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword),
                    VB.Syntax.ParameterList(VB.Syntax.Token(VB.SyntaxKind.OpenParenToken), SeparatedList(parameter), VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)),
                    null);
                if (node.Body.Kind == CS.SyntaxKind.Block)
                {
                    return VB.Syntax.MultiLineFunctionLambdaExpression(
                        lambdaHeader,
                        StatementTerminator(),
                        statementVisitor.VisitStatement((CS.BlockSyntax)node.Body),
                        VB.Syntax.EndFunctionStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword)));
                }
                else
                {
                    return VB.Syntax.SingleLineFunctionLambdaExpression(
                        lambdaHeader,
                        VB.Syntax.Token(VB.SyntaxKind.StatementTerminatorToken, string.Empty),
                        Visit<VB.SyntaxNode>(node.Body));
                }
            }

            public override VB.SyntaxNode VisitConditionalExpression(CS.ConditionalExpressionSyntax node)
            {
                var argumentsArray = new VB.ArgumentSyntax[] 
                    { 
                        VB.Syntax.SimpleArgument(VisitExpression(node.Condition)),
                        VB.Syntax.SimpleArgument(VisitExpression(node.WhenTrue)),
                        VB.Syntax.SimpleArgument(VisitExpression(node.WhenFalse))
                    };
                var arguments = SeparatedCommaList(argumentsArray);
                var argumentList = VB.Syntax.ArgumentList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    arguments,
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));

                return VB.Syntax.InvocationExpression(
                    VB.Syntax.IdentifierName(VB.Syntax.Identifier("If")),
                    argumentList);
            }

            public override VB.SyntaxNode VisitElementAccessExpression(CS.ElementAccessExpressionSyntax node)
            {
                return VB.Syntax.InvocationExpression(
                    VisitExpression(node.Expression),
                    Visit<VB.ArgumentListSyntax>(node.ArgumentList));
            }

            public override VB.SyntaxNode VisitParenthesizedExpression(CS.ParenthesizedExpressionSyntax node)
            {
                return VB.Syntax.ParenthesizedExpression(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VisitExpression(node.Expression),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitImplicitArrayCreationExpression(CS.ImplicitArrayCreationExpressionSyntax node)
            {
                return Visit<VB.CollectionInitializerSyntax>(node.Initializer);
            }

            public override VB.SyntaxNode VisitInitializerExpression(CS.InitializerExpressionSyntax node)
            {
                if (node.Parent.Kind == CS.SyntaxKind.AnonymousObjectCreationExpression)
                {
                    var fieldInitializers = new List<VB.FieldInitializerSyntax>();
                    foreach (var expression in node.Expressions)
                    {
                        if (expression.Kind == CS.SyntaxKind.AssignExpression)
                        {
                            var assignment = (CS.BinaryExpressionSyntax)expression;
                            if (assignment.Left.Kind == CS.SyntaxKind.IdentifierName)
                            {
                                fieldInitializers.Add(VB.Syntax.NamedFieldInitializer(
                                    new VB.SyntaxToken(),
                                    VB.Syntax.Token(VB.SyntaxKind.DotToken),
                                    VB.Syntax.IdentifierName(ConvertIdentifier((CS.IdentifierNameSyntax)assignment.Left)),
                                    VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                    VisitExpression(assignment.Right)));
                                continue;
                            }
                        }

                        fieldInitializers.Add(VB.Syntax.InferredFieldInitializer(
                            new VB.SyntaxToken(), VisitExpression(expression)));
                    }

                    return VB.Syntax.ObjectMemberInitializer(
                        VB.Syntax.Token(VB.SyntaxKind.WithKeyword),
                        VB.Syntax.Token(VB.SyntaxKind.OpenBraceToken),
                        SeparatedCommaList(fieldInitializers),
                        VB.Syntax.Token(VB.SyntaxKind.CloseBraceToken));
                }
                else if (node.Parent.Kind == CS.SyntaxKind.ObjectCreationExpression)
                {
                    if (node.Expressions.Count > 0 &&
                        node.Expressions[0].Kind == CS.SyntaxKind.AssignExpression)
                    {
                        var initializers = new List<VB.FieldInitializerSyntax>();
                        foreach (var e in node.Expressions)
                        {
                            if (e.Kind == CS.SyntaxKind.AssignExpression)
                            {
                                var binary = (CS.BinaryExpressionSyntax)e;
                                if (binary.Left.Kind == CS.SyntaxKind.IdentifierName)
                                {
                                    initializers.Add(
                                        VB.Syntax.NamedFieldInitializer(
                                            new VB.SyntaxToken(),
                                            VB.Syntax.Token(VB.SyntaxKind.DotToken),
                                            VB.Syntax.IdentifierName(ConvertIdentifier((CS.IdentifierNameSyntax)binary.Left)),
                                            VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                            VisitExpression(binary.Right)));
                                    continue;
                                }
                            }

                            initializers.Add(
                                VB.Syntax.InferredFieldInitializer(
                                    new VB.SyntaxToken(),
                                    VisitExpression(e)));
                        }

                        return VB.Syntax.ObjectMemberInitializer(
                            VB.Syntax.Token(VB.SyntaxKind.WithKeyword),
                            VB.Syntax.Token(VB.SyntaxKind.OpenBraceToken),
                            SeparatedCommaList(initializers),
                            VB.Syntax.Token(VB.SyntaxKind.CloseBraceToken));
                    }
                    else
                    {
                        return VB.Syntax.ObjectCollectionInitializer(
                            VB.Syntax.Token(VB.SyntaxKind.FromKeyword),
                            VB.Syntax.CollectionInitializer(
                                VB.Syntax.Token(VB.SyntaxKind.OpenBraceToken),
                                SeparatedCommaList(node.Expressions.Select(VisitExpression).ToList()),
                                VB.Syntax.Token(VB.SyntaxKind.CloseBraceToken)));
                    }
                }
                else
                {
                    return VB.Syntax.CollectionInitializer(
                            VB.Syntax.Token(VB.SyntaxKind.OpenBraceToken),
                            SeparatedCommaList(node.Expressions.Select(VisitExpression).ToList()),
                            VB.Syntax.Token(VB.SyntaxKind.CloseBraceToken));
                }
            }

            public override VB.SyntaxNode VisitForEachStatement(CS.ForEachStatementSyntax node)
            {
                var begin = VB.Syntax.ForEachStatement(
                    VB.Syntax.Token(VB.SyntaxKind.ForKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.EachKeyword),
                    VB.Syntax.IdentifierName(ConvertIdentifier(node.Identifier)),
                    VB.Syntax.Token(VB.SyntaxKind.InKeyword),
                    VisitExpression(node.Expression));
                return VB.Syntax.ForEachBlock(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Statement),
                    VB.Syntax.NextStatement(VB.Syntax.Token(VB.SyntaxKind.NextKeyword), SeparatedList<VB.ExpressionSyntax>(null)));
            }

            public override VB.SyntaxNode VisitAttributeList(CS.AttributeListSyntax node)
            {
                return VB.Syntax.AttributeList(
                    VB.Syntax.Token(VB.SyntaxKind.LessThanToken),
                    SeparatedCommaList(node.Attributes.Select(Visit<VB.AttributeSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.GreaterThanToken));
            }

            public override VB.SyntaxNode VisitAttribute(CS.AttributeSyntax node)
            {
                var parent = (CS.AttributeListSyntax)node.Parent;
                return VB.Syntax.Attribute(
                    Visit<VB.AttributeTargetSyntax>(parent.Target),
                    VisitType(node.Name),
                    Visit<VB.ArgumentListSyntax>(node.ArgumentList));
            }

            public override VB.SyntaxNode VisitAttributeTargetSpecifier(CS.AttributeTargetSpecifierSyntax node)
            {
                // todo: any other types of attribute targets (like 'return', etc.) 
                // should cause us to actually move the attribute to a different 
                // location in the VB signature.
                //
                // For now, we only handle assembly/module.

                switch (node.Identifier.ValueText)
                {
                    default:
                        return null;
                    case "assembly":
                    case "module":
                        var modifier = VisitToken(node.Identifier);
                        return VB.Syntax.AttributeTarget(
                            modifier,
                            VB.Syntax.Token(VB.SyntaxKind.ColonToken));
                }
            }

            public override VB.SyntaxNode VisitAttributeArgumentList(CS.AttributeArgumentListSyntax node)
            {
                return VB.Syntax.ArgumentList(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    SeparatedCommaList(node.Arguments.Select(Visit<VB.ArgumentSyntax>)),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitAttributeArgument(CS.AttributeArgumentSyntax node)
            {
                if (node.NameEquals == null)
                {
                    return VB.Syntax.SimpleArgument(VisitExpression(node.Expression));
                }
                else
                {
                    return VB.Syntax.NamedArgument(
                        VB.Syntax.IdentifierName(ConvertIdentifier(node.NameEquals.Name)),
                        VB.Syntax.Token(VB.SyntaxKind.ColonEqualsToken),
                        VisitExpression(node.Expression));
                }
            }

            public override VB.SyntaxNode VisitPropertyDeclaration(CS.PropertyDeclarationSyntax node)
            {
                var modifiers = ConvertModifiers(node.Modifiers);
                if (node.AccessorList.Accessors.Count == 1)
                {
                    if (node.AccessorList.Accessors[0].Keyword.Kind == CS.SyntaxKind.GetKeyword)
                    {
                        modifiers = TokenList(modifiers.Concat(VB.Syntax.Token(VB.SyntaxKind.ReadOnlyKeyword)));
                    }
                    else
                    {
                        modifiers = TokenList(modifiers.Concat(VB.Syntax.Token(VB.SyntaxKind.WriteOnlyKeyword)));
                    }
                }

                var begin = VB.Syntax.PropertyStatement(
                    ConvertAttributes(node.AttributeLists),
                    modifiers,
                    VB.Syntax.Token(VB.SyntaxKind.PropertyKeyword),
                    ConvertIdentifier(node.Identifier),
                    null,
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                    null,
                    null);

                if (node.AccessorList.Accessors.All(a => a.Body == null))
                {
                    return begin;
                }

                return VB.Syntax.PropertyBlock(
                    begin,
                    StatementTerminator(),
                    SeparatedNewLineList(node.AccessorList.Accessors.Select(Visit<VB.MethodBlockSyntax>)),
                    VB.Syntax.EndPropertyStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.PropertyKeyword)));
            }

            public override VB.SyntaxNode VisitIndexerDeclaration(CS.IndexerDeclarationSyntax node)
            {
                var begin = VB.Syntax.PropertyStatement(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    VB.Syntax.Token(VB.SyntaxKind.PropertyKeyword),
                    VB.Syntax.Identifier("Item"),
                    Visit<VB.ParameterListSyntax>(node.ParameterList),
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                    null,
                    null);

                return VB.Syntax.PropertyBlock(
                    begin,
                    StatementTerminator(),
                    SeparatedNewLineList(node.AccessorList.Accessors.Select(Visit<VB.MethodBlockSyntax>)),
                    VB.Syntax.EndPropertyStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.PropertyKeyword)));
            }

            public override VB.SyntaxNode VisitAccessorDeclaration(CS.AccessorDeclarationSyntax node)
            {
                var attributes = ConvertAttributes(node.AttributeLists);
                var modifiers = ConvertModifiers(node.Modifiers);
                var body = statementVisitor.VisitStatement(node.Body);

                switch (node.Kind)
                {
                    case CS.SyntaxKind.AddAccessorDeclaration:
                        {
                            var begin = VB.Syntax.AddHandlerAccessorStatement(
                                attributes,
                                modifiers,
                                VB.Syntax.Token(VB.SyntaxKind.AddHandlerKeyword),
                                null,
                                null);

                            return VB.Syntax.AddHandlerBlock(
                                begin,
                                StatementTerminator(),
                                body,
                                VB.Syntax.EndAddHandlerStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.AddHandlerKeyword)));
                        }

                    case CS.SyntaxKind.GetAccessorDeclaration:
                        {
                            var begin = VB.Syntax.GetAccessorStatement(
                                attributes,
                                modifiers,
                                VB.Syntax.Token(VB.SyntaxKind.GetKeyword),
                                null,
                                null);

                            return VB.Syntax.PropertyGetBlock(
                                begin,
                                StatementTerminator(),
                                body,
                                VB.Syntax.EndGetStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.GetKeyword)));
                        }

                    case CS.SyntaxKind.RemoveAccessorDeclaration:
                        {
                            var begin = VB.Syntax.RemoveHandlerAccessorStatement(
                                attributes,
                                modifiers,
                                VB.Syntax.Token(VB.SyntaxKind.RemoveHandlerKeyword),
                                null,
                                null);

                            return VB.Syntax.RemoveHandlerBlock(
                                begin,
                                StatementTerminator(),
                                body,
                                VB.Syntax.EndRemoveHandlerStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.RemoveHandlerKeyword)));
                        }

                    case CS.SyntaxKind.SetAccessorDeclaration:
                        {
                            var begin = VB.Syntax.SetAccessorStatement(
                                attributes,
                                modifiers,
                                VB.Syntax.Token(VB.SyntaxKind.SetKeyword),
                                null,
                                null);

                            return VB.Syntax.PropertySetBlock(
                                begin,
                                StatementTerminator(),
                                body,
                                VB.Syntax.EndSetStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.SetKeyword)));
                        }

                    default:
                        throw new NotImplementedException();
                }
            }

            // private static readonly Regex SpaceSlashSlashSlashRegex =
            //    new Regex("^(\\s*)///(.*)$", RegexOptions.Compiled | RegexOptions.Singleline);
            // private static readonly Regex SpaceStarRegex =
            //    new Regex("^(\\s*)((/\\*\\*)|(\\*)|(\\*/))(.*)$", RegexOptions.Compiled | RegexOptions.Singleline);
            // private static readonly char[] LineSeparators = { '\r', '\n', '\u0085', '\u2028', '\u2029' };

            public override VB.SyntaxNode VisitDocumentationCommentTrivia(CS.DocumentationCommentTriviaSyntax node)
            {
                var text = string.Join("\r\n", node.GetInteriorXml().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                                                                    .Select(s => "'''" + s));
                var root = VB.SyntaxTree.ParseText(text).GetRoot() as VB.CompilationUnitSyntax;
                return root.EndOfFileToken.LeadingTrivia[0].GetStructure();
                /*
                var text = node.ToFullString();

                var lines = text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();

                Regex regex = null;
                if (text.StartsWith("///"))
                {
                    regex = SpaceSlashSlashSlashRegex;
                }
                else if (text.StartsWith("/**"))
                {
                    regex = SpaceStarRegex;
                }
                else
                {
                    throw new NotImplementedException();
                }

                lines.Do(line =>
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        sb.Append(match.Groups[1].Value);
                        sb.Append("'''");
                        sb.AppendLine(match.Groups[match.Groups.Count - 1].Value);
                    }
                    else
                    {
                        sb.Append("'''");
                        sb.AppendLine(line);
                    }
                });

                return VB.Syntax.ParseTrailingTrivia(sb.ToString()).FirstOrDefault();
                 */
            }

            public override VB.SyntaxNode VisitArrayType(CS.ArrayTypeSyntax node)
            {
                return VB.Syntax.ArrayType(
                    VisitType(node.ElementType),
                    List(node.RankSpecifiers.Select(Visit<VB.ArrayRankSpecifierSyntax>)));
            }

            public override VB.SyntaxNode VisitArrayRankSpecifier(CS.ArrayRankSpecifierSyntax node)
            {
                // TODO: pass the right number of commas
                return VB.Syntax.ArrayRankSpecifier(
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitArrayCreationExpression(CS.ArrayCreationExpressionSyntax node)
            {
                var initializer = Visit<VB.CollectionInitializerSyntax>(node.Initializer);
                if (initializer == null)
                {
                    initializer = VB.Syntax.CollectionInitializer(
                        VB.Syntax.Token(VB.SyntaxKind.OpenBraceToken),
                        SeparatedList<VB.ExpressionSyntax>(null),
                        VB.Syntax.Token(VB.SyntaxKind.CloseBraceToken));
                }

                var arrayType = (VB.ArrayTypeSyntax)VisitType(node.Type);

                return VB.Syntax.ArrayCreationExpression(
                    VB.Syntax.Token(VB.SyntaxKind.NewKeyword),
                    null,
                    arrayType.ElementType,
                    null,
                    arrayType.RankSpecifiers,
                    initializer);
            }

            public override VB.SyntaxNode VisitVariableDeclaration(CS.VariableDeclarationSyntax node)
            {
                return VB.Syntax.LocalDeclarationStatement(
                    VB.Syntax.TokenList(VB.Syntax.Token(VB.SyntaxKind.DimKeyword)),
                    SeparatedNewLineList(node.Variables.Select(Visit<VB.VariableDeclaratorSyntax>)));
            }

            public override VB.SyntaxNode VisitPostfixUnaryExpression(CS.PostfixUnaryExpressionSyntax node)
            {
                var val1 = VisitExpression(node.Operand);
                var val2 = VisitExpression(node.Operand);

                switch (node.Kind)
                {
                    case CS.SyntaxKind.PostIncrementExpression:
                        return VB.Syntax.AssignmentStatement(
                            val1,
                            VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                            VB.Syntax.AddExpression(val2, VB.Syntax.Token(VB.SyntaxKind.PlusToken),
                                VB.Syntax.NumericLiteralExpression(VB.Syntax.IntegerLiteralToken("1", VB.LiteralBase.Decimal, VB.TypeCharacter.None, 1))));
                    case CS.SyntaxKind.PostDecrementExpression:
                        return VB.Syntax.AssignmentStatement(
                            val1,
                            VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                            VB.Syntax.SubtractExpression(val2, VB.Syntax.Token(VB.SyntaxKind.MinusToken),
                                VB.Syntax.NumericLiteralExpression(VB.Syntax.IntegerLiteralToken("1", VB.LiteralBase.Decimal, VB.TypeCharacter.None, 1))));
                }

                throw new NotImplementedException();
            }

            public override VB.SyntaxNode VisitOperatorDeclaration(CS.OperatorDeclarationSyntax node)
            {
                VB.SyntaxToken @operator;
                switch (node.OperatorToken.Kind)
                {
                    case CS.SyntaxKind.AmpersandToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.AndKeyword);
                        break;
                    case CS.SyntaxKind.AmpersandAmpersandToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.AndAlsoKeyword);
                        break;
                    case CS.SyntaxKind.AsteriskToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.AsteriskToken);
                        break;
                    case CS.SyntaxKind.BarToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.OrKeyword);
                        break;
                    case CS.SyntaxKind.CaretToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.XorKeyword);
                        break;
                    case CS.SyntaxKind.MinusToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.MinusToken);
                        break;
                    case CS.SyntaxKind.MinusMinusToken:
                        @operator = VB.Syntax.Identifier("Decrement");
                        break;
                    case CS.SyntaxKind.EqualsEqualsToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.EqualsToken);
                        break;
                    case CS.SyntaxKind.FalseKeyword:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.IsFalseKeyword);
                        break;
                    case CS.SyntaxKind.ExclamationToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.NotKeyword);
                        break;
                    case CS.SyntaxKind.ExclamationEqualsToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.LessThanGreaterThanToken);
                        break;
                    case CS.SyntaxKind.GreaterThanToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.GreaterThanToken);
                        break;
                    case CS.SyntaxKind.GreaterThanGreaterThanToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.GreaterThanGreaterThanToken);
                        break;
                    case CS.SyntaxKind.GreaterThanEqualsToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.GreaterThanEqualsToken);
                        break;
                    case CS.SyntaxKind.LessThanToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.LessThanToken);
                        break;
                    case CS.SyntaxKind.LessThanEqualsToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.LessThanEqualsToken);
                        break;
                    case CS.SyntaxKind.LessThanLessThanToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.LessThanLessThanToken);
                        break;
                    case CS.SyntaxKind.PercentToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.ModKeyword);
                        break;
                    case CS.SyntaxKind.PlusToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.PlusToken);
                        break;
                    case CS.SyntaxKind.PlusPlusToken:
                        @operator = VB.Syntax.Identifier("Increment");
                        break;
                    case CS.SyntaxKind.SlashToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.SlashToken);
                        break;
                    case CS.SyntaxKind.TildeToken:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.NotKeyword);
                        break;
                    case CS.SyntaxKind.TrueKeyword:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.IsTrueKeyword);
                        break;
                    case CS.SyntaxKind.ExplicitKeyword:
                    case CS.SyntaxKind.ImplicitKeyword:
                    case CS.SyntaxKind.None:
                        @operator = VB.Syntax.Token(VB.SyntaxKind.EmptyToken, node.OperatorToken.ToString());
                        break;
                    default:
                        throw new NotImplementedException();
                }

                VB.SyntaxList<VB.AttributeListSyntax> returnAttributes;
                VB.SyntaxList<VB.AttributeListSyntax> remainingAttributes;
                SplitAttributes(node.AttributeLists.ToList(), out returnAttributes, out remainingAttributes);

                var begin = VB.Syntax.OperatorStatement(
                    remainingAttributes,
                    ConvertModifiers(node.Modifiers),
                    VB.Syntax.Token(VB.SyntaxKind.OperatorKeyword),
                    @operator,
                    Visit<VB.ParameterListSyntax>(node.ParameterList),
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), returnAttributes, VisitType(node.ReturnType)));

                if (node.Body == null)
                {
                    return begin;
                }

                return VB.Syntax.OperatorBlock(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Body),
                    VB.Syntax.EndOperatorStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.OperatorKeyword)));
            }

            public override VB.SyntaxNode VisitPrefixUnaryExpression(CS.PrefixUnaryExpressionSyntax node)
            {
                switch (node.Kind)
                {
                    case CS.SyntaxKind.AddressOfExpression:
                        return VB.Syntax.AddressOfExpression(
                            VB.Syntax.Token(VB.SyntaxKind.AddressOfKeyword),
                            VisitExpression(node.Operand));
                    case CS.SyntaxKind.BitwiseNotExpression:
                    case CS.SyntaxKind.LogicalNotExpression:
                        return VB.Syntax.NotExpression(
                            VB.Syntax.Token(VB.SyntaxKind.NotKeyword),
                            VisitExpression(node.Operand));
                    case CS.SyntaxKind.NegateExpression:
                        return VB.Syntax.NegateExpression(
                            VB.Syntax.Token(VB.SyntaxKind.MinusToken),
                            VisitExpression(node.Operand));
                    case CS.SyntaxKind.PlusExpression:
                        return VB.Syntax.PlusExpression(
                            VB.Syntax.Token(VB.SyntaxKind.PlusToken),
                            VisitExpression(node.Operand));
                    case CS.SyntaxKind.PointerIndirectionExpression:
                        return Visit<VB.SyntaxNode>(node.Operand);
                    case CS.SyntaxKind.PreDecrementExpression:
                        {
                            var val1 = VisitExpression(node.Operand);
                            var val2 = VisitExpression(node.Operand);
                            return VB.Syntax.AssignmentStatement(
                                val1,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.SubtractExpression(val2, VB.Syntax.Token(VB.SyntaxKind.MinusToken), VB.Syntax.NumericLiteralExpression(
                                    VB.Syntax.IntegerLiteralToken("1", VB.LiteralBase.Decimal, VB.TypeCharacter.None, 1))));
                        }

                    case CS.SyntaxKind.PreIncrementExpression:
                        {
                            var val1 = VisitExpression(node.Operand);
                            var val2 = VisitExpression(node.Operand);
                            return VB.Syntax.AssignmentStatement(
                                val1,
                                VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                                VB.Syntax.AddExpression(val2, VB.Syntax.Token(VB.SyntaxKind.PlusToken), VB.Syntax.NumericLiteralExpression(
                                    VB.Syntax.IntegerLiteralToken("1", VB.LiteralBase.Decimal, VB.TypeCharacter.None, 1))));
                        }

                    default:
                        throw new NotImplementedException();
                }
            }

            public override VB.SyntaxNode VisitDefaultExpression(CS.DefaultExpressionSyntax node)
            {
                return VB.Syntax.NothingLiteralExpression(VB.Syntax.Token(VB.SyntaxKind.NothingKeyword));
            }

            public override VB.SyntaxNode VisitTypeOfExpression(CS.TypeOfExpressionSyntax node)
            {
                return VB.Syntax.GetTypeExpression(
                    VB.Syntax.Token(VB.SyntaxKind.GetTypeKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                    VisitType(node.Type),
                    VB.Syntax.Token(VB.SyntaxKind.CloseParenToken));
            }

            public override VB.SyntaxNode VisitCheckedExpression(CS.CheckedExpressionSyntax node)
            {
                string functionName;
                switch (node.Kind)
                {
                    case CS.SyntaxKind.CheckedExpression:
                        functionName = "Checked";
                        break;
                    case CS.SyntaxKind.UncheckedExpression:
                        functionName = "Unchecked";
                        break;
                    default:
                        throw new NotImplementedException();
                }

                return VB.Syntax.InvocationExpression(
                    VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(functionName, functionName)),
                    VB.Syntax.ArgumentList(
                        VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                        SingleSeparatedList<VB.ArgumentSyntax>(
                            VB.Syntax.SimpleArgument(VisitExpression(node.Expression))),
                        VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
            }

            public override VB.SyntaxNode VisitMakeRefExpression(CS.MakeRefExpressionSyntax node)
            {
                string functionName = "MakeRef";
                return VB.Syntax.InvocationExpression(
                    VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(functionName, functionName)),
                    VB.Syntax.ArgumentList(
                        VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                        SingleSeparatedList<VB.ArgumentSyntax>(
                            VB.Syntax.SimpleArgument(VisitExpression(node.Expression))),
                        VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
            }

            public override VB.SyntaxNode VisitRefTypeExpression(CS.RefTypeExpressionSyntax node)
            {
                string functionName = "RefType";
                return VB.Syntax.InvocationExpression(
                    VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(functionName, functionName)),
                    VB.Syntax.ArgumentList(
                        VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                        SingleSeparatedList<VB.ArgumentSyntax>(
                            VB.Syntax.SimpleArgument(VisitExpression(node.Expression))),
                        VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
            }

            public override VB.SyntaxNode VisitRefValueExpression(CS.RefValueExpressionSyntax node)
            {
                string functionName = "RefValue";
                return VB.Syntax.InvocationExpression(
                    VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(functionName, functionName)),
                    VB.Syntax.ArgumentList(
                        VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                        SingleSeparatedList<VB.ArgumentSyntax>(
                            VB.Syntax.SimpleArgument(VisitExpression(node.Expression))),
                        VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
            }

            public override VB.SyntaxNode VisitSizeOfExpression(CS.SizeOfExpressionSyntax node)
            {
                string functionName = "SizeOf";
                return VB.Syntax.InvocationExpression(
                    VB.Syntax.StringLiteralExpression(VB.Syntax.StringLiteralToken(functionName, functionName)),
                    VB.Syntax.ArgumentList(
                        VB.Syntax.Token(VB.SyntaxKind.OpenParenToken),
                        SingleSeparatedList<VB.ArgumentSyntax>(
                            VB.Syntax.SimpleArgument(VisitType(node.Type))),
                        VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)));
            }

            public override VB.SyntaxNode VisitBadDirectiveTrivia(CS.BadDirectiveTriviaSyntax node)
            {
                var comment = VB.Syntax.CommentTrivia(CreateCouldNotBeConvertedComment(node.ToFullString(), typeof(VB.DirectiveTriviaSyntax)));
                var directive = VB.Syntax.BadDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken).WithTrailingTrivia(comment));
                return VB.Syntax.DirectiveTrivia(
                    directive,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitWarningDirectiveTrivia(CS.WarningDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitErrorDirectiveTrivia(CS.ErrorDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitRegionDirectiveTrivia(CS.RegionDirectiveTriviaSyntax node)
            {
                var region = VB.Syntax.RegionDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    VB.Syntax.Token(VB.SyntaxKind.RegionKeyword),
                    VB.Syntax.StringLiteralToken(node.EndOfDirectiveToken.ToString(), node.EndOfDirectiveToken.ToString()));
                return VB.Syntax.DirectiveTrivia(
                    region,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitEndRegionDirectiveTrivia(CS.EndRegionDirectiveTriviaSyntax node)
            {
                var endregion = VB.Syntax.EndRegionDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    VB.Syntax.Token(VB.SyntaxKind.EndKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.RegionKeyword));
                return VB.Syntax.DirectiveTrivia(
                    endregion,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitEndIfDirectiveTrivia(CS.EndIfDirectiveTriviaSyntax node)
            {
                var endifDirective = VB.Syntax.EndIfDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    VB.Syntax.Token(VB.SyntaxKind.EndKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.IfKeyword));
                return VB.Syntax.DirectiveTrivia(
                    endifDirective,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitElseDirectiveTrivia(CS.ElseDirectiveTriviaSyntax node)
            {
                var elseDirective = VB.Syntax.ElseDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    VB.Syntax.Token(VB.SyntaxKind.ElseKeyword));
                return VB.Syntax.DirectiveTrivia(
                    elseDirective,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitIfDirectiveTrivia(CS.IfDirectiveTriviaSyntax node)
            {
                var ifDirective = VB.Syntax.IfDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    new VB.SyntaxToken(),
                    VB.Syntax.Token(VB.SyntaxKind.IfKeyword),
                    VisitExpression(node.Condition),
                    new VB.SyntaxToken());
                return VB.Syntax.DirectiveTrivia(
                    ifDirective,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitElifDirectiveTrivia(CS.ElifDirectiveTriviaSyntax node)
            {
                var elifDirective = VB.Syntax.ElseIfDirective(
                    VB.Syntax.Token(VB.SyntaxKind.HashToken),
                    new VB.SyntaxToken(),
                    VB.Syntax.Token(VB.SyntaxKind.ElseIfKeyword),
                    VisitExpression(node.Condition),
                    new VB.SyntaxToken());
                return VB.Syntax.DirectiveTrivia(
                    elifDirective,
                    StatementTerminator());
            }

            public override VB.SyntaxNode VisitEnumMemberDeclaration(CS.EnumMemberDeclarationSyntax node)
            {
                var expression = node.EqualsValue == null ? null : VisitExpression(node.EqualsValue.Value);
                var initializer = expression == null ? null : VB.Syntax.EqualsValue(
                    VB.Syntax.Token(VB.SyntaxKind.EqualsToken),
                    expression);
                return VB.Syntax.EnumMemberDeclaration(
                    ConvertAttributes(node.AttributeLists),
                    ConvertIdentifier(node.Identifier),
                    initializer);
            }

            public override VB.SyntaxNode VisitNullableType(CS.NullableTypeSyntax node)
            {
                return VB.Syntax.NullableType(
                    VisitType(node.ElementType),
                    VB.Syntax.Token(VB.SyntaxKind.QuestionToken));
            }

            public override VB.SyntaxNode VisitAnonymousMethodExpression(CS.AnonymousMethodExpressionSyntax node)
            {
                var begin = VB.Syntax.FunctionLambdaHeader(
                    new VB.SyntaxList<VB.AttributeListSyntax>(),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword),
                    Visit<VB.ParameterListSyntax>(node.ParameterList),
                    null);
                return VB.Syntax.MultiLineFunctionLambdaExpression(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Block),
                    VB.Syntax.EndFunctionStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword)));
            }

            public override VB.SyntaxNode VisitQueryExpression(CS.QueryExpressionSyntax node)
            {
                IEnumerable<VB.QueryClauseSyntax> newClauses =
                    Enumerable.Repeat(Visit<VB.QueryClauseSyntax>(node.FromClause), 1)
                    .Concat(node.Body.Clauses.Select(Visit<VB.QueryClauseSyntax>))
                    .Concat(Visit<VB.QueryClauseSyntax>(node.Body.SelectOrGroup));
                return VB.Syntax.QueryExpression(List(newClauses));
            }

            public override VB.SyntaxNode VisitSelectClause(CS.SelectClauseSyntax node)
            {
                var expression = VisitExpression(node.Expression);
                var expressionRange = VB.Syntax.ExpressionRangeVariable(null, expression);
                return VB.Syntax.SelectClause(
                    VB.Syntax.Token(VB.SyntaxKind.SelectKeyword),
                    SeparatedList(expressionRange));
            }

            public override VB.SyntaxNode VisitFromClause(CS.FromClauseSyntax node)
            {
                var initializer = VB.Syntax.CollectionRangeVariable(
                    VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()),
                    node.Type == null ? null : VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                    VB.Syntax.Token(VB.SyntaxKind.InKeyword),
                    VisitExpression(node.Expression));

                return VB.Syntax.FromClause(
                    VB.Syntax.Token(VB.SyntaxKind.FromKeyword),
                    SeparatedList(initializer));
            }

            public override VB.SyntaxNode VisitOrderByClause(CS.OrderByClauseSyntax node)
            {
                return VB.Syntax.OrderByClause(
                    VB.Syntax.Token(VB.SyntaxKind.OrderKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.ByKeyword),
                    SeparatedCommaList(node.Orderings.Select(Visit<VB.OrderingSyntax>)));
            }

            public override VB.SyntaxNode VisitOrdering(CS.OrderingSyntax node)
            {
                if (node.AscendingOrDescendingKeyword.Kind == CS.SyntaxKind.None)
                {
                    return VB.Syntax.AscendingOrdering(VisitExpression(node.Expression), new VB.SyntaxToken());
                }
                else if (node.AscendingOrDescendingKeyword.Kind == CS.SyntaxKind.AscendingKeyword)
                {
                    return VB.Syntax.AscendingOrdering(VisitExpression(node.Expression), VB.Syntax.Token(VB.SyntaxKind.AscendingKeyword));
                }
                else
                {
                    return VB.Syntax.DescendingOrdering(VisitExpression(node.Expression), VB.Syntax.Token(VB.SyntaxKind.DescendingKeyword));
                }
            }

            public override VB.SyntaxNode VisitWhereClause(CS.WhereClauseSyntax node)
            {
                return VB.Syntax.WhereClause(
                    VB.Syntax.Token(VB.SyntaxKind.WhereKeyword),
                    VisitExpression(node.Condition));
            }

            public override VB.SyntaxNode VisitJoinClause(CS.JoinClauseSyntax node)
            {
                if (node.Into == null)
                {
                    return VB.Syntax.JoinClause(
                        VB.Syntax.Token(VB.SyntaxKind.JoinKeyword),
                        SeparatedList(VB.Syntax.CollectionRangeVariable(
                            VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()),
                            node.Type == null ? null : VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                            VB.Syntax.Token(VB.SyntaxKind.InKeyword),
                            VisitExpression(node.InExpression))),
                            null,
                            VB.Syntax.Token(VB.SyntaxKind.OnKeyword),
                            SeparatedList(VB.Syntax.JoinCondition(
                                VisitExpression(node.LeftExpression),
                                VB.Syntax.Token(VB.SyntaxKind.EqualsKeyword),
                                VisitExpression(node.RightExpression))));
                }
                else
                {
                    return VB.Syntax.GroupJoinClause(
                        VB.Syntax.Token(VB.SyntaxKind.GroupKeyword, "Group"),
                        VB.Syntax.Token(VB.SyntaxKind.JoinKeyword),
                        SeparatedList(VB.Syntax.CollectionRangeVariable(
                            VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()),
                            node.Type == null ? null : VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                            VB.Syntax.Token(VB.SyntaxKind.InKeyword),
                            VisitExpression(node.InExpression))),
                            null,
                            VB.Syntax.Token(VB.SyntaxKind.OnKeyword),
                            SeparatedList(VB.Syntax.JoinCondition(
                                VisitExpression(node.LeftExpression),
                                VB.Syntax.Token(VB.SyntaxKind.EqualsKeyword),
                                VisitExpression(node.RightExpression))),
                            VB.Syntax.Token(VB.SyntaxKind.IntoKeyword),
                            SeparatedList(VB.Syntax.AggregationRangeVariable(
                                VB.Syntax.VariableNameEquals(VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Into.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()), null, VB.Syntax.Token(VB.SyntaxKind.EqualsToken)),
                                VB.Syntax.GroupAggregation(VB.Syntax.Token(VB.SyntaxKind.GroupKeyword, "Group")))));
                }
            }

            public override VB.SyntaxNode VisitGroupClause(CS.GroupClauseSyntax node)
            {
                var groupExpression = VB.Syntax.ExpressionRangeVariable(
                    null, VisitExpression(node.GroupExpression));
                var byExpression = VB.Syntax.ExpressionRangeVariable(
                    null, VisitExpression(node.ByExpression));
                var query = (CS.QueryExpressionSyntax)node.Parent;
                VB.AggregationRangeVariableSyntax rangeVariable;
                if (query.Body.Continuation == null)
                {
                    rangeVariable = VB.Syntax.AggregationRangeVariable(
                          null,
                          VB.Syntax.GroupAggregation(VB.Syntax.Token(VB.SyntaxKind.GroupKeyword, "Group")));
                }
                else
                {
                    rangeVariable = VB.Syntax.AggregationRangeVariable(
                          VB.Syntax.VariableNameEquals(VB.Syntax.ModifiedIdentifier(ConvertIdentifier(query.Body.Continuation.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()), null, VB.Syntax.Token(VB.SyntaxKind.EqualsToken)),
                          VB.Syntax.GroupAggregation(VB.Syntax.Token(VB.SyntaxKind.GroupKeyword, "Group")));
                }

                return VB.Syntax.GroupByClause(
                    VB.Syntax.Token(VB.SyntaxKind.GroupKeyword, "Group"),
                    SeparatedList(groupExpression),
                    VB.Syntax.Token(VB.SyntaxKind.ByKeyword),
                    SeparatedList(byExpression),
                    VB.Syntax.Token(VB.SyntaxKind.IntoKeyword),
                    SeparatedList(rangeVariable));
            }

            public override VB.SyntaxNode VisitLetClause(CS.LetClauseSyntax node)
            {
                return VB.Syntax.LetClause(
                    VB.Syntax.Token(VB.SyntaxKind.LetKeyword),
                    SeparatedList(
                        VB.Syntax.ExpressionRangeVariable(
                            VB.Syntax.VariableNameEquals(VB.Syntax.ModifiedIdentifier(ConvertIdentifier(node.Identifier), new VB.SyntaxToken(), null, new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>()), null, VB.Syntax.Token(VB.SyntaxKind.EqualsToken)),
                            VisitExpression(node.Expression))));
            }

            public override VB.SyntaxNode VisitAnonymousObjectCreationExpression(CS.AnonymousObjectCreationExpressionSyntax node)
            {
                return VB.Syntax.AnonymousObjectCreationExpression(
                    VB.Syntax.Token(VB.SyntaxKind.NewKeyword),
                    null,
                    VB.Syntax.ObjectMemberInitializer(
                        node.Initializers.Select(Visit<VB.FieldInitializerSyntax>).ToArray()));
            }

            public override SyntaxNode VisitAnonymousObjectMemberDeclarator(CS.AnonymousObjectMemberDeclaratorSyntax node)
            {
                return node.NameEquals == null
                    ? VB.Syntax.InferredFieldInitializer(VisitExpression(node.Expression))
                    : (VB.FieldInitializerSyntax)VB.Syntax.NamedFieldInitializer(VB.Syntax.IdentifierName(ConvertIdentifier(node.NameEquals.Name)), VisitExpression(node.Expression));
            }

            public override VB.SyntaxNode VisitDefineDirectiveTrivia(CS.DefineDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitUndefDirectiveTrivia(CS.UndefDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitPragmaWarningDirectiveTrivia(CS.PragmaWarningDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitPragmaChecksumDirectiveTrivia(CS.PragmaChecksumDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitLineDirectiveTrivia(CS.LineDirectiveTriviaSyntax node)
            {
                return CreateBadDirective(node, this);
            }

            public override VB.SyntaxNode VisitFinallyClause(CS.FinallyClauseSyntax node)
            {
                return VB.Syntax.FinallyPart(
                    VB.Syntax.FinallyStatement(VB.Syntax.Token(VB.SyntaxKind.FinallyKeyword)),
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Block));
            }

            public override VB.SyntaxNode VisitCatchClause(CS.CatchClauseSyntax node)
            {
                VB.CatchStatementSyntax statement;
                if (node.Declaration == null)
                {
                    statement = VB.Syntax.CatchStatement(
                        VB.Syntax.Token(VB.SyntaxKind.CatchKeyword),
                        null,
                        null,
                        null);
                }
                else if (node.Declaration.Identifier.Kind == CS.SyntaxKind.None)
                {
                    statement = VB.Syntax.CatchStatement(
                        VB.Syntax.Token(VB.SyntaxKind.CatchKeyword),
                        null,
                        VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Declaration.Type)),
                        null);
                }
                else
                {
                    statement = VB.Syntax.CatchStatement(
                        VB.Syntax.Token(VB.SyntaxKind.CatchKeyword),
                        VB.Syntax.IdentifierName(ConvertIdentifier(node.Declaration.Identifier)),
                        VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Declaration.Type)),
                        null);
                }

                return VB.Syntax.CatchPart(
                    statement,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Block));
            }

            public override VB.SyntaxNode VisitConversionOperatorDeclaration(CS.ConversionOperatorDeclarationSyntax node)
            {
                var direction = node.Modifiers.Any(t => t.Kind == CS.SyntaxKind.ImplicitKeyword)
                    ? VB.Syntax.Token(VB.SyntaxKind.WideningKeyword)
                    : VB.Syntax.Token(VB.SyntaxKind.NarrowingKeyword);

                var begin = VB.Syntax.OperatorStatement(
                    ConvertAttributes(node.AttributeLists),
                    TokenList(ConvertModifiers(node.Modifiers).Concat(direction)),
                    VB.Syntax.Token(VB.SyntaxKind.OperatorKeyword),
                    VB.Syntax.Token(VB.SyntaxKind.CTypeKeyword),
                    Visit<VB.ParameterListSyntax>(node.ParameterList),
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)));

                if (node.Body == null)
                {
                    return begin;
                }

                return VB.Syntax.OperatorBlock(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Body),
                    VB.Syntax.EndOperatorStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.OperatorKeyword)));
            }

            public override VB.SyntaxNode VisitPointerType(CS.PointerTypeSyntax node)
            {
                // just ignore the pointer part
                return Visit<VB.SyntaxNode>(node.ElementType);
            }

            public override VB.SyntaxNode VisitDestructorDeclaration(CS.DestructorDeclarationSyntax node)
            {
                var begin = VB.Syntax.SubStatement(
                    new VB.SyntaxList<VB.AttributeListSyntax>(),
                    new VB.SyntaxTokenList(),
                    VB.Syntax.Token(VB.SyntaxKind.SubKeyword),
                    VB.Syntax.Identifier("Finalize"),
                    null,
                    VB.Syntax.ParameterList(VB.Syntax.Token(VB.SyntaxKind.OpenParenToken), SeparatedList<VB.ParameterSyntax>(null), VB.Syntax.Token(VB.SyntaxKind.CloseParenToken)),
                    null,
                    null,
                    null);

                if (node.Body == null)
                {
                    return begin;
                }

                return VB.Syntax.SubBlock(
                    begin,
                    StatementTerminator(),
                    statementVisitor.VisitStatement(node.Body),
                    VB.Syntax.EndSubStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.SubKeyword)));
            }

            public override VB.SyntaxNode VisitDelegateDeclaration(CS.DelegateDeclarationSyntax node)
            {
                VB.SyntaxToken identifier = ConvertIdentifier(node.Identifier);
                VB.TypeParameterListSyntax typeParameters = Visit<VB.TypeParameterListSyntax>(node.TypeParameterList);

                if (node.ReturnType.Kind == CS.SyntaxKind.PredefinedType &&
                    ((CS.PredefinedTypeSyntax)node.ReturnType).Keyword.Kind == CS.SyntaxKind.VoidKeyword)
                {
                    return VB.Syntax.DelegateSubStatement(
                        ConvertAttributes(node.AttributeLists),
                        ConvertModifiers(node.Modifiers),
                        VB.Syntax.Token(VB.SyntaxKind.DelegateKeyword),
                        VB.Syntax.Token(VB.SyntaxKind.SubKeyword),
                        identifier,
                        typeParameters,
                        Visit<VB.ParameterListSyntax>(node.ParameterList),
                        null);
                }
                else
                {
                    return VB.Syntax.DelegateFunctionStatement(
                        ConvertAttributes(node.AttributeLists),
                        ConvertModifiers(node.Modifiers),
                        VB.Syntax.Token(VB.SyntaxKind.DelegateKeyword),
                        VB.Syntax.Token(VB.SyntaxKind.FunctionKeyword),
                        identifier,
                        typeParameters,
                        Visit<VB.ParameterListSyntax>(node.ParameterList),
                        VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.ReturnType)));
                }
            }

            public override VB.SyntaxNode VisitEventFieldDeclaration(CS.EventFieldDeclarationSyntax node)
            {
                return VB.Syntax.EventStatement(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    new VB.SyntaxToken(),
                    VB.Syntax.Token(VB.SyntaxKind.EventKeyword),
                    ConvertIdentifier(node.Declaration.Variables[0].Identifier),
                    null,
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Declaration.Type)),
                    null);
            }

            public override VB.SyntaxNode VisitEventDeclaration(CS.EventDeclarationSyntax node)
            {
                VB.SyntaxToken identifier = ConvertIdentifier(node.Identifier);

                var begin = VB.Syntax.EventStatement(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    new VB.SyntaxToken(),
                    VB.Syntax.Token(VB.SyntaxKind.EventKeyword),
                    identifier,
                    null,
                    VB.Syntax.SimpleAsClause(VB.Syntax.Token(VB.SyntaxKind.AsKeyword), new VB.SyntaxList<VB.AttributeListSyntax>(), VisitType(node.Type)),
                    null);

                return VB.Syntax.EventBlock(
                    begin,
                    StatementTerminator(),
                    SeparatedNewLineList(node.AccessorList.Accessors.Select(Visit<VB.MethodBlockSyntax>)),
                    VB.Syntax.EndEventStatement(VB.Syntax.Token(VB.SyntaxKind.EndKeyword), VB.Syntax.Token(VB.SyntaxKind.EventKeyword)));
            }

            public override VB.SyntaxNode VisitStackAllocArrayCreationExpression(CS.StackAllocArrayCreationExpressionSyntax node)
            {
                var error = CreateCouldNotBeConvertedString(node.ToFullString(), typeof(VB.SyntaxNode));
                return VB.Syntax.StringLiteralExpression(
                    VB.Syntax.StringLiteralToken(error, error));
            }

            public override VB.SyntaxNode VisitIncompleteMember(CS.IncompleteMemberSyntax node)
            {
                return VB.Syntax.FieldDeclaration(
                    ConvertAttributes(node.AttributeLists),
                    ConvertModifiers(node.Modifiers),
                    SeparatedList(
                        VB.Syntax.VariableDeclarator(
                            SeparatedList(VB.Syntax.ModifiedIdentifier(
                                VB.Syntax.Identifier("IncompleteMember"),
                                new VB.SyntaxToken(),
                                null,
                                new VB.SyntaxList<VB.ArrayRankSpecifierSyntax>())),
                            VB.Syntax.SimpleAsClause(
                                VB.Syntax.Token(VB.SyntaxKind.AsKeyword),
                                new VB.SyntaxList<VB.AttributeListSyntax>(),
                                VisitType(node.Type)), null)));
            }

            public override VB.SyntaxNode VisitExternAliasDirective(CS.ExternAliasDirectiveSyntax node)
            {
                var leadingTrivia = node.GetFirstToken(includeSkipped: true).LeadingTrivia.SelectMany(VisitTrivia);
                var trailingTrivia = node.GetLastToken(includeSkipped: true).TrailingTrivia.SelectMany(VisitTrivia);

                var comment = VB.Syntax.CommentTrivia(
                    CreateCouldNotBeConvertedComment(node.ToString(), typeof(VB.ImportsStatementSyntax)));
                leadingTrivia = leadingTrivia.Concat(comment);

                return VB.Syntax.ImportsStatement(
                    VB.Syntax.Token(TriviaList(leadingTrivia), VB.SyntaxKind.ImportsKeyword, TriviaList(trailingTrivia), string.Empty),
                    new VB.SeparatedSyntaxList<VB.ImportsClauseSyntax>());
            }

            public override VB.SyntaxNode VisitConstructorInitializer(CS.ConstructorInitializerSyntax node)
            {
                VB.InstanceExpressionSyntax expr = null;
                if (node.IsKind(CS.SyntaxKind.BaseConstructorInitializer))
                {
                    expr = VB.Syntax.MyBaseExpression();
                }
                else
                {
                    expr = VB.Syntax.MyClassExpression();
                }

                var invocation = VB.Syntax.InvocationExpression(
                    VB.Syntax.MemberAccessExpression(
                        expr,
                        VB.Syntax.Token(VB.SyntaxKind.DotToken),
                        VB.Syntax.IdentifierName(VB.Syntax.Identifier("New"))),
                        Visit<VB.ArgumentListSyntax>(node.ArgumentList));

                return VB.Syntax.CallStatement(invocation: invocation);
            }

            public override VB.SyntaxNode DefaultVisit(CS.SyntaxNode node)
            {
                // If you hit this, it means there was some sort of CS construct
                // that we haven't written a conversion routine for.  Simply add
                // it above and rerun.
                throw new NotImplementedException();
            }
        }
    }
}
