' *********************************************************
'
' Copyright � Microsoft Corporation
'
' Licensed under the Apache License, Version 2.0 (the
' "License"); you may not use this file except in
' compliance with the License. You may obtain a copy of
' the License at
'
' http://www.apache.org/licenses/LICENSE-2.0 
'
' THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES
' OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
' INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES
' OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR
' PURPOSE, MERCHANTABLITY OR NON-INFRINGEMENT.
'
' See the Apache 2 License for the specific language
' governing permissions and limitations under the License.
'
' *********************************************************

Imports Roslyn.Compilers.CSharp.Syntax
Imports Roslyn.Compilers.CSharp.SyntaxExtensions
Imports CS = Roslyn.Compilers.CSharp
Imports VB = Roslyn.Compilers.VisualBasic
Imports Extension = System.Runtime.CompilerServices.ExtensionAttribute

Namespace VisualBasicToCSharpConverter.Converting

    Partial Public Class Converter

        Private Class NodeConvertingVisitor
            Inherits VB.SyntaxVisitor(Of CS.SyntaxNode)

            Private Shared ReadOnly VoidKeyword As CS.SyntaxToken = Token(CS.SyntaxKind.VoidKeyword)
            Private Shared ReadOnly SemicolonToken As CS.SyntaxToken = Token(CS.SyntaxKind.SemicolonToken)
            Private Shared ReadOnly MissingSemicolonToken As CS.SyntaxToken = MissingToken(CS.SyntaxKind.SemicolonToken)
            ' This is a hack. But because this will be written out to text it'll work.
            Private Shared ReadOnly SystemRuntimeInteropServicesCharSetName As CS.NameSyntax = ParseName("global::System.Runtime.InteropServices.CharSet")
            Private Shared ReadOnly SystemRuntimeInteropServicesCharSetAnsiExpression As CS.MemberAccessExpressionSyntax = MemberAccessExpression(CS.SyntaxKind.MemberAccessExpression, SystemRuntimeInteropServicesCharSetName, IdentifierName("Ansi"))
            Private Shared ReadOnly SystemRuntimeInteropServicesCharSetUnicodeExpression As CS.MemberAccessExpressionSyntax = MemberAccessExpression(CS.SyntaxKind.MemberAccessExpression, SystemRuntimeInteropServicesCharSetName, IdentifierName("Unicode"))
            Private Shared ReadOnly SystemRuntimeInteropServicesCharSetAutoExpression As CS.MemberAccessExpressionSyntax = MemberAccessExpression(CS.SyntaxKind.MemberAccessExpression, SystemRuntimeInteropServicesCharSetName, IdentifierName("Auto"))

            ' This can change after visiting an OptionStatement.
            Private IsOptionExplicitOn As Boolean = True
            Private IsOptionCompareBinary As Boolean = True
            Private IsOptionStrictOn As Boolean = False
            Private IsOptionInferOn As Boolean = True

            Private ReadOnly RootNamespace As String = ""
            Private ReadOnly RootNamespaceName As CS.NameSyntax = If(String.IsNullOrEmpty(RootNamespace),
                                                                     Nothing,
                                                                     ParseName(RootNamespace)
                                                                  )

            Protected Function DeriveName(expression As VB.ExpressionSyntax) As String
                Do While TypeOf expression Is VB.InvocationExpressionSyntax
                    expression = CType(expression, VB.InvocationExpressionSyntax).Expression
                Loop

                Select Case expression.Kind
                    Case VB.SyntaxKind.MemberAccessExpression

                        Return CType(expression, VB.MemberAccessExpressionSyntax).Name.Identifier.ValueText

                    Case VB.SyntaxKind.IdentifierName

                        Return CType(expression, VB.IdentifierNameSyntax).Identifier.ValueText

                    Case VB.SyntaxKind.GenericName

                        Return CType(expression, VB.GenericNameSyntax).Identifier.ValueText

                    Case Else
                        Return Nothing
                End Select
            End Function

            Protected Function DeriveRankSpecifiers(
                                   boundsOpt As VB.ArgumentListSyntax,
                                   specifiersOpt As IEnumerable(Of VB.ArrayRankSpecifierSyntax),
                                   Optional includeSizes As Boolean = False
                               ) As IEnumerable(Of CS.ArrayRankSpecifierSyntax)

                Dim result As New List(Of CS.ArrayRankSpecifierSyntax)()

                If boundsOpt IsNot Nothing Then
                    If includeSizes Then
                        result.Add(ArrayRankSpecifier((From arg In boundsOpt.Arguments Select VisitArrayRankSpecifierSize(arg)).ToCommaSeparatedList(Of CS.ExpressionSyntax)))
                    Else
                        result.Add(ArrayRankSpecifier(OmittedArraySizeExpressionList(Of CS.ExpressionSyntax)(boundsOpt.Arguments.Count)))
                    End If
                End If

                If specifiersOpt IsNot Nothing Then
                    For Each ars In specifiersOpt
                        result.Add(ArrayRankSpecifier(OmittedArraySizeExpressionList(Of CS.ExpressionSyntax)(ars.Rank)))
                    Next
                End If

                Return result

            End Function

            Protected Function DeriveInitializer(
                                   identifier As VB.ModifiedIdentifierSyntax,
                                   asClauseOpt As VB.AsClauseSyntax,
                                   Optional initializerOpt As VB.EqualsValueSyntax = Nothing
                               ) As CS.EqualsValueClauseSyntax

                If initializerOpt IsNot Nothing Then
                    Return Visit(initializerOpt)
                End If

                If asClauseOpt IsNot Nothing AndAlso asClauseOpt.Kind = Roslyn.Compilers.VisualBasic.SyntaxKind.AsNewClause Then
                    Dim newExpression = DirectCast(asClauseOpt, VB.AsNewClauseSyntax).NewExpression
                    Select Case newExpression.Kind
                        Case Roslyn.Compilers.VisualBasic.SyntaxKind.ObjectCreationExpression
                            Return EqualsValueClause(VisitObjectCreationExpression(newExpression))
                        Case Roslyn.Compilers.VisualBasic.SyntaxKind.ArrayCreationExpression
                            Return EqualsValueClause(VisitArrayCreationExpression(newExpression))
                        Case Roslyn.Compilers.VisualBasic.SyntaxKind.AnonymousObjectCreationExpression
                            Return EqualsValueClause(VisitAnonymousObjectCreationExpression(newExpression))
                    End Select
                End If

                If identifier.ArrayBounds IsNot Nothing Then
                    Return EqualsValueClause(ArrayCreationExpression(DeriveType(identifier, asClauseOpt, initializerOpt, includeSizes:=True)))
                End If

                Return Nothing

            End Function

            Protected Function DeriveType(
                                   identifier As VB.ModifiedIdentifierSyntax,
                                   asClause As VB.AsClauseSyntax,
                                   initializer As VB.EqualsValueSyntax,
                                   Optional includeSizes As Boolean = False,
                                   Optional isRangeVariable As Boolean = False
                               ) As CS.TypeSyntax

                Dim type = DeriveType(identifier.Identifier, asClause, , initializer, isRangeVariable)

                ' TODO: Implement check for nullable var.
                If identifier.Nullable.Kind <> VB.SyntaxKind.None Then
                    type = NullableType(type)
                End If

                If identifier.ArrayBounds IsNot Nothing OrElse
                   identifier.ArrayRankSpecifiers.Count > 0 Then

                    Return ArrayType(type, List(DeriveRankSpecifiers(identifier.ArrayBounds, identifier.ArrayRankSpecifiers, includeSizes)))
                End If

                Return type
            End Function

            Protected Function DeriveType(
                                   identifier As VB.SyntaxToken,
                                   asClause As VB.AsClauseSyntax,
                                   Optional methodKeyword As VB.SyntaxToken = Nothing,
                                   Optional initializerOpt As VB.EqualsValueSyntax = Nothing,
                                   Optional isRangeVariable As Boolean = False
                               ) As CS.TypeSyntax

                If asClause IsNot Nothing Then

                    If asClause.Kind = VB.SyntaxKind.AsNewClause Then
                        Return IdentifierName("var")
                    Else
                        Return Visit(asClause)
                    End If

                ElseIf methodKeyword.Kind = VB.SyntaxKind.SubKeyword Then

                    Return PredefinedType(Token(CS.SyntaxKind.VoidKeyword))

                ElseIf initializerOpt IsNot Nothing AndAlso
                       IsOptionInferOn AndAlso
                       (identifier.Parent.Parent.Parent.Kind = VB.SyntaxKind.LocalDeclarationStatement OrElse
                        identifier.Parent.Parent.Parent.Kind = VB.SyntaxKind.UsingStatement) Then

                    Return IdentifierName("var")

                ElseIf isRangeVariable Then

                    ' C# collection range variables omit their type.
                    Return Nothing

                Else
                    Dim text = identifier.ToString()

                    If Not Char.IsLetterOrDigit(text(text.Length - 1)) Then

                        Select Case text(text.Length - 1)
                            Case "!"c
                                Return PredefinedType(Token(CS.SyntaxKind.FloatKeyword))
                            Case "@"c
                                Return PredefinedType(Token(CS.SyntaxKind.DecimalKeyword))
                            Case "#"c
                                Return PredefinedType(Token(CS.SyntaxKind.DoubleKeyword))
                            Case "$"c
                                Return PredefinedType(Token(CS.SyntaxKind.StringKeyword))
                            Case "%"c
                                Return PredefinedType(Token(CS.SyntaxKind.IntKeyword))
                            Case "&"c
                                Return PredefinedType(Token(CS.SyntaxKind.LongKeyword))
                        End Select
                    End If
                End If

                ' If no AsClause is provided and no type characters are present and this isn't a Sub declaration pick Object or Dynamic based on Option Strict setting.
                If IsOptionStrictOn Then
                    Return PredefinedType(Token(CS.SyntaxKind.ObjectKeyword))
                Else
                    Return IdentifierName("dynamic")
                End If

            End Function

            Protected Function DeriveType(declarator As VB.CollectionRangeVariableSyntax) As CS.TypeSyntax

                Return DeriveType(declarator.Identifier, declarator.AsClause, initializer:=Nothing, isRangeVariable:=True)

            End Function

            Protected Function TransferTrivia(source As VB.SyntaxNode, target As CS.SyntaxNode) As CS.SyntaxNode

                Return target.WithTrivia(VisitTrivia(source.GetLeadingTrivia()), VisitTrivia(source.GetTrailingTrivia()))

            End Function

            Public Overloads Function Visit(nodes As IEnumerable(Of VB.SyntaxNode)) As IEnumerable(Of CS.SyntaxNode)

                Return From node In nodes Select Visit(node)

            End Function

            Public Overloads Function Visit(statements As IEnumerable(Of VB.StatementSyntax)) As IEnumerable(Of CS.SyntaxNode)

                ' VB variable declarations allow multiple types and variables. In order to translate to proper C# code
                ' we have to flatten the list here by raising each declarator to the level of its parent.
                Return Aggregate
                           node In statements
                       Into
                           SelectMany(Flatten(node))


            End Function

            Function Flatten(statement As VB.SyntaxNode) As IEnumerable(Of CS.SyntaxNode)

                Select Case statement.Kind
                    Case VB.SyntaxKind.FieldDeclaration
                        Return Aggregate node In CType(statement, VB.FieldDeclarationSyntax).Declarators Into SelectMany(VisitVariableDeclaratorVariables(node))
                    Case VB.SyntaxKind.LocalDeclarationStatement
                        Return Aggregate node In CType(statement, VB.LocalDeclarationStatementSyntax).Declarators Into SelectMany(VisitVariableDeclaratorVariables(node))
                    Case Else
                        Return {Visit(statement)}
                End Select

            End Function

            Public Overrides Function VisitAccessorStatement(node As VB.AccessorStatementSyntax) As CS.SyntaxNode

                Dim accessorBlock As VB.MethodBlockSyntax = node.Parent

                Dim kind As CS.SyntaxKind
                Select Case node.Kind
                    Case VB.SyntaxKind.GetAccessorStatement
                        kind = CS.SyntaxKind.GetAccessorDeclaration
                    Case VB.SyntaxKind.SetAccessorStatement
                        kind = CS.SyntaxKind.SetAccessorDeclaration
                    Case VB.SyntaxKind.AddHandlerAccessorStatement
                        kind = CS.SyntaxKind.AddAccessorDeclaration
                    Case VB.SyntaxKind.RemoveHandlerAccessorStatement
                        kind = CS.SyntaxKind.RemoveAccessorDeclaration
                    Case VB.SyntaxKind.RaiseEventHandlerAccessorStatement

                        ' TODO: Transform RaiseEvent accessor into a method.
                        Throw New NotImplementedException()

                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

                Return TransferTrivia(accessorBlock, AccessorDeclaration(kind).WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))).WithModifiers(TokenList(VisitModifiers(node.Modifiers))).WithBody(Block(List(Visit(accessorBlock.Statements)))))

            End Function

            Public Overrides Function VisitAddRemoveHandlerStatement(node As VB.AddRemoveHandlerStatementSyntax) As CS.SyntaxNode

                If node.Kind = VB.SyntaxKind.AddHandlerStatement Then
                    Return TransferTrivia(node, ExpressionStatement(BinaryExpression(CS.SyntaxKind.AddAssignExpression, Visit(node.EventExpression), Visit(node.DelegateExpression))))
                Else
                    Return TransferTrivia(node, ExpressionStatement(BinaryExpression(CS.SyntaxKind.SubtractAssignExpression, Visit(node.EventExpression), Visit(node.DelegateExpression))))
                End If

            End Function

            Public Overrides Function VisitAliasImportsClause(node As VB.AliasImportsClauseSyntax) As CS.SyntaxNode

                Return TransferTrivia(node.Parent, UsingDirective(Visit(node.Name)).WithAlias(NameEquals(CS.Syntax.IdentifierName(VisitIdentifier(node.Alias)))))

            End Function

            Public Overrides Function VisitAnonymousObjectCreationExpression(node As VB.AnonymousObjectCreationExpressionSyntax) As CS.SyntaxNode

                Return AnonymousObjectCreationExpression(Visit(node.Initializer.Initializers).ToCommaSeparatedList(Of CS.AnonymousObjectMemberDeclaratorSyntax)())

            End Function

            Public Overrides Function VisitArgumentList(node As VB.ArgumentListSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return ArgumentList()

                Return ArgumentList(Visit(node.Arguments).ToCommaSeparatedList(Of CS.ArgumentSyntax)())

            End Function

            Public Overrides Function VisitArrayCreationExpression(node As VB.ArrayCreationExpressionSyntax) As CS.SyntaxNode

                Return ArrayCreationExpression(ArrayType(Visit(node.Type)) _
                        .WithRankSpecifiers(List(DeriveRankSpecifiers(node.ArrayBounds, node.RankSpecifiers, includeSizes:=True)))) _
                        .WithInitializer(If(node.ArrayBounds IsNot Nothing AndAlso node.Initializer.Initializers.Count = 0, Nothing, VisitCollectionInitializer(node.Initializer)))

            End Function

            Public Overrides Function VisitArrayRankSpecifier(node As VB.ArrayRankSpecifierSyntax) As CS.SyntaxNode

                Return ArrayRankSpecifier(OmittedArraySizeExpressionList(Of CS.ExpressionSyntax)(node.Rank))

            End Function

            Protected Overloads Function VisitArrayRankSpecifierSize(node As VB.ArgumentSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                If TypeOf node Is VB.RangeArgumentSyntax Then
                    Dim arg As VB.RangeArgumentSyntax = node

                    Return VisitArrayBound(arg.UpperBound)
                Else
                    Return VisitArrayBound(CType(node, VB.SimpleArgumentSyntax).Expression)
                End If

            End Function

            Protected Function VisitArrayBound(expression As VB.SyntaxNode) As CS.SyntaxNode

                If expression.Kind = VB.SyntaxKind.SubtractExpression Then
                    Dim be As VB.BinaryExpressionSyntax = expression

                    If be.Right.Kind = VB.SyntaxKind.NumericLiteralExpression Then
                        If CInt(CType(be.Right, VB.LiteralExpressionSyntax).Token.Value) = 1 Then
                            Return Visit(be.Left)
                        End If
                    End If

                ElseIf expression.Kind = VB.SyntaxKind.NumericLiteralExpression Then

                    ' Practically speaking this can only legally be -1.
                    Dim length = CInt(CType(expression, VB.LiteralExpressionSyntax).Token.Value) + 1

                    Return LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, Literal(CStr(length), length))

                ElseIf expression.Kind = VB.SyntaxKind.NegateExpression Then

                    Dim negate As VB.UnaryExpressionSyntax = expression
                    If negate.Operand.Kind = VB.SyntaxKind.NumericLiteralExpression Then
                        Dim length = -CInt(CType(negate.Operand, VB.LiteralExpressionSyntax).Token.Value) + 1

                        Return LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, Literal(CStr(length), length))
                    End If

                End If

                Return BinaryExpression(
                           CS.SyntaxKind.AddExpression,
                           ParenthesizedExpression(Visit(expression)),
                           LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, Literal("1", 1))
                       )

            End Function

            Public Overrides Function VisitArrayType(node As VB.ArrayTypeSyntax) As CS.SyntaxNode

                Return ArrayType(Visit(node.ElementType), List(DeriveRankSpecifiers(Nothing, node.RankSpecifiers.ToList())))

            End Function

            Public Overrides Function VisitAssignmentStatement(node As VB.AssignmentStatementSyntax) As CS.SyntaxNode

                Return TransferTrivia(node, ExpressionStatement(BinaryExpression(CS.SyntaxKind.AssignExpression, Visit(node.Left), Visit(node.Right))))

            End Function

            Public Overrides Function VisitAttribute(node As VB.AttributeSyntax) As CS.SyntaxNode

                Return TransferTrivia(node.Parent, AttributeList({Attribute(Visit(node.Name), VisitAttributeArgumentList(node.ArgumentList))}.ToCommaSeparatedList(Of CS.AttributeSyntax)()).WithTarget(VisitAttributeTarget(node.Target)))

            End Function

            Protected Function VisitAttributeArgumentList(node As VB.ArgumentListSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return AttributeArgumentList(Visit(node.Arguments).ToCommaSeparatedList(Of CS.AttributeArgumentSyntax)())

            End Function

            Public Overrides Function VisitAttributeList(node As VB.AttributeListSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Protected Function VisitAttributeLists(nodes As IEnumerable(Of VB.AttributeListSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return Visit((From list In nodes, attribute In list.Attributes Select attribute))

            End Function

            Public Overrides Function VisitAttributesStatement(node As VB.AttributesStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Protected Function VisitAttributeStatements(statements As IEnumerable(Of VB.AttributesStatementSyntax)) As IEnumerable(Of CS.SyntaxNode)

                ' TOOD: AttributeStatement contains a list of blocks but there is only ever one block in the list.
                Return Visit((From statement In statements, list In statement.AttributeLists, attribute In list.Attributes Select attribute))

            End Function

            Public Overrides Function VisitAttributeTarget(node As VB.AttributeTargetSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                If node.AttributeModifier.Kind = VB.SyntaxKind.AssemblyKeyword Then
                    Return AttributeTargetSpecifier(Token(CS.SyntaxKind.AssemblyKeyword))
                Else
                    Return AttributeTargetSpecifier(Token(CS.SyntaxKind.ModuleKeyword))
                End If

            End Function

            Public Overrides Function VisitBadDirective(node As VB.BadDirectiveSyntax) As CS.SyntaxNode

                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitBinaryConditionalExpression(node As VB.BinaryConditionalExpressionSyntax) As CS.SyntaxNode

                Return BinaryExpression(CS.SyntaxKind.CoalesceExpression, Visit(node.FirstExpression), Visit(node.SecondExpression))

            End Function

            Public Overrides Function VisitBinaryExpression(node As VB.BinaryExpressionSyntax) As CS.SyntaxNode

                Dim kind As CS.SyntaxKind

                Select Case node.Kind
                    Case VB.SyntaxKind.AddExpression
                        kind = CS.SyntaxKind.AddExpression
                    Case VB.SyntaxKind.SubtractExpression
                        kind = CS.SyntaxKind.SubtractExpression
                    Case VB.SyntaxKind.MultiplyExpression
                        kind = CS.SyntaxKind.MultiplyExpression
                    Case VB.SyntaxKind.DivideExpression

                        kind = CS.SyntaxKind.DivideExpression
                        ' TODO: Transform into cast with division if needed.

                    Case VB.SyntaxKind.ModuloExpression
                        kind = CS.SyntaxKind.ModuloExpression

                    Case VB.SyntaxKind.IntegerDivideExpression

                        kind = CS.SyntaxKind.DivideExpression
                        ' TODO: Transform into user-defined operator method call if needed.

                    Case VB.SyntaxKind.PowerExpression

                        'TODO: Transform into call to Math.Pow.
                        Return ExpressionStatement(
                                   InvocationExpression(
                                       ParseName("global::System.Math.Pow"),
                                       ArgumentList(
                                           {CS.Syntax.Argument(Visit(node.Left)),
                                            CS.Syntax.Argument(Visit(node.Right))
                                           }.ToCommaSeparatedList()
                                       )
                                   )
                               )

                        Return NotImplementedExpression(node)

                        Throw New NotImplementedException(node.ToString())

                    Case VB.SyntaxKind.EqualsExpression
                        kind = CS.SyntaxKind.EqualsExpression
                    Case VB.SyntaxKind.NotEqualsExpression
                        kind = CS.SyntaxKind.NotEqualsExpression
                    Case VB.SyntaxKind.LessThanExpression
                        kind = CS.SyntaxKind.LessThanExpression
                    Case VB.SyntaxKind.LessThanOrEqualExpression
                        kind = CS.SyntaxKind.LessThanOrEqualExpression
                    Case VB.SyntaxKind.GreaterThanExpression
                        kind = CS.SyntaxKind.GreaterThanExpression
                    Case VB.SyntaxKind.GreaterThanOrEqualExpression
                        kind = CS.SyntaxKind.GreaterThanOrEqualExpression

                    Case VB.SyntaxKind.IsExpression

                        ' TODO: Transform into call to Object.ReferenceEquals as necessary.
                        kind = CS.SyntaxKind.EqualsExpression

                    Case VB.SyntaxKind.IsNotExpression

                        ' TODO: Transform into NotExpression of call to Object.ReferenceEquals as necessary.
                        kind = CS.SyntaxKind.NotEqualsExpression

                    Case VB.SyntaxKind.LeftShiftExpression
                        kind = CS.SyntaxKind.LeftShiftExpression
                    Case VB.SyntaxKind.RightShiftExpression
                        kind = CS.SyntaxKind.RightShiftExpression
                    Case VB.SyntaxKind.AndExpression
                        kind = CS.SyntaxKind.BitwiseAndExpression
                    Case VB.SyntaxKind.AndAlsoExpression
                        kind = CS.SyntaxKind.LogicalAndExpression
                    Case VB.SyntaxKind.OrExpression
                        kind = CS.SyntaxKind.BitwiseOrExpression
                    Case VB.SyntaxKind.OrElseExpression
                        kind = CS.SyntaxKind.LogicalOrExpression
                    Case VB.SyntaxKind.XorExpression
                        kind = CS.SyntaxKind.ExclusiveOrExpression

                    Case VB.SyntaxKind.ConcatenateExpression

                        kind = CS.SyntaxKind.AddExpression

                        ' TODO: Transform into call to user-defined operator if needed (e.g. for user-defined operator).

                    Case VB.SyntaxKind.LikeExpression

                        Return NotImplementedExpression(node)

                        Throw New NotSupportedException(node.Kind.ToString())

                    Case Else

                        Return NotImplementedExpression(node)

                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

                Return BinaryExpression(kind, Visit(node.Left), Visit(node.Right))
            End Function

            Public Overrides Function VisitCallStatement(node As VB.CallStatementSyntax) As CS.SyntaxNode

                Return TransferTrivia(node, ExpressionStatement(VisitInvocationExpression(node.Invocation)))

            End Function

            Public Overrides Function VisitCaseBlock(node As VB.CaseBlockSyntax) As CS.SyntaxNode

                Dim statements = Visit(node.Statements)
                If node.Kind <> VB.SyntaxKind.CaseElseBlock Then
                    statements = statements.Union({BreakStatement()})
                End If

                Return TransferTrivia(node, SwitchSection(
                                                List(Visit(node.Begin.Cases)),
                                                List(statements)
                                            )
                       )

            End Function

            Protected Function VisitCaseBlocks(blocks As IEnumerable(Of VB.CaseBlockSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return From b In blocks Select VisitCaseBlock(b)

            End Function

            Public Overrides Function VisitCaseElseClause(node As VB.CaseElseClauseSyntax) As CS.SyntaxNode

                Return SwitchLabel(CS.SyntaxKind.DefaultSwitchLabel)

            End Function

            Public Overrides Function VisitCaseRangeClause(node As VB.CaseRangeClauseSyntax) As CS.SyntaxNode

                Return SwitchLabel(CS.SyntaxKind.CaseSwitchLabel, MissingToken(CS.SyntaxKind.CaseKeyword), NotImplementedExpression(node), MissingToken(CS.SyntaxKind.ColonToken))

                ' TODO: Rewrite this to an if statement.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitCaseRelationalClause(node As VB.CaseRelationalClauseSyntax) As CS.SyntaxNode

                Return SwitchLabel(CS.SyntaxKind.CaseSwitchLabel, MissingToken(CS.SyntaxKind.CaseKeyword), NotImplementedExpression(node), MissingToken(CS.SyntaxKind.ColonToken))

                ' TODO: Rewrite this to an if statement.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitCaseStatement(node As VB.CaseStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitCaseValueClause(node As VB.CaseValueClauseSyntax) As CS.SyntaxNode

                Return SwitchLabel(CS.SyntaxKind.CaseSwitchLabel, Visit(node.Value))

            End Function

            Public Overrides Function VisitDirectCastExpression(node As Roslyn.Compilers.VisualBasic.DirectCastExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return ParenthesizedExpression(CastExpression(Visit(node.Type), Visit(node.Expression)))
            End Function

            Public Overrides Function VisitCTypeExpression(node As Roslyn.Compilers.VisualBasic.CTypeExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return ParenthesizedExpression(CastExpression(Visit(node.Type), Visit(node.Expression)))
            End Function

            Public Overrides Function VisitTryCastExpression(node As Roslyn.Compilers.VisualBasic.TryCastExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return ParenthesizedExpression(BinaryExpression(CS.SyntaxKind.AsExpression, Visit(node.Expression), Visit(node.Type)))
            End Function

            Public Overrides Function VisitCatchFilterClause(node As VB.CatchFilterClauseSyntax) As CS.SyntaxNode

                ' We could in theory translate this into a switch inside a catch.
                ' It's not really at all the same thing as a filter though so for now
                ' we'll just throw.
                Throw New NotSupportedException(node.Kind.ToString())

            End Function

            Public Overrides Function VisitCatchPart(node As VB.CatchPartSyntax) As CS.SyntaxNode

                Return CatchClause().WithDeclaration(VisitCatchStatement(node.Begin)).WithBlock(Block(List(Visit(node.Statements))))

            End Function

            Public Overrides Function VisitCatchStatement(node As VB.CatchStatementSyntax) As CS.SyntaxNode


                If node.IdentifierName Is Nothing Then Return Nothing

                Dim result = CatchDeclaration(VisitSimpleAsClause(node.AsClause)).WithIdentifier(VisitIdentifier(node.IdentifierName.Identifier))

                If node.WhenClause IsNot Nothing Then result = result.WithTrailingTrivia({Comment("/* " & node.WhenClause.ToString() & " */")})

                Return result

            End Function

            Protected Function VisitCatchParts(parts As IEnumerable(Of VB.CatchPartSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return From part In parts Select VisitCatchPart(part)

            End Function

            Public Overrides Function VisitCollectionInitializer(node As VB.CollectionInitializerSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Select Case node.Parent.Kind
                    Case VB.SyntaxKind.ObjectCollectionInitializer,
                            VB.SyntaxKind.AsNewClause
                        Return InitializerExpression(CS.SyntaxKind.CollectionInitializerExpression, Visit(node.Initializers).ToCommaSeparatedList(Of CS.ExpressionSyntax)())

                    Case VB.SyntaxKind.ArrayCreationExpression
                        Return InitializerExpression(CS.SyntaxKind.ArrayInitializerExpression, Visit(node.Initializers).ToCommaSeparatedList(Of CS.ExpressionSyntax)())

                    Case Else

                        ' This covers array initializers in a variable declaration.
                        If node.Parent.Kind = VB.SyntaxKind.EqualsValue AndAlso
                           node.Parent.Parent.Kind = VB.SyntaxKind.VariableDeclarator AndAlso
                           CType(node.Parent.Parent, VB.VariableDeclaratorSyntax).AsClause IsNot Nothing Then

                            Return InitializerExpression(CS.SyntaxKind.ArrayInitializerExpression, Visit(node.Initializers).ToCommaSeparatedList(Of CS.ExpressionSyntax)())
                        End If

                        ' This is an array literal.
                        ' TODO: Calculate the rank of this array initializer, right now it assumes rank 1.
                        Return ImplicitArrayCreationExpression(
                                   InitializerExpression(CS.SyntaxKind.ArrayInitializerExpression, Visit(node.Initializers).ToCommaSeparatedList(Of CS.ExpressionSyntax)())
                               )
                End Select

            End Function

            Public Overrides Function VisitCollectionRangeVariable(node As VB.CollectionRangeVariableSyntax) As CS.SyntaxNode
                Return MyBase.VisitCollectionRangeVariable(node)
            End Function

            Public Overrides Function VisitCompilationUnit(node As VB.CompilationUnitSyntax) As CS.SyntaxNode
                Dim usings = List(VisitImportsStatements(node.Imports))
                Dim attributes = List(VisitAttributeStatements(node.Attributes))
                Dim members = List(VisitMembers(node.Members))
                Dim root = CompilationUnit().WithUsings(usings).WithAttributeLists(attributes).WithMembers(members)

                Return NormalizeWhitespace(root)
            End Function

            Public Overrides Function VisitConstDirective(node As VB.ConstDirectiveSyntax) As CS.SyntaxNode

                If node.Value.Kind = VB.SyntaxKind.TrueLiteralExpression OrElse
                   node.Value.Kind = VB.SyntaxKind.FalseLiteralExpression Then

                    Return DefineDirectiveTrivia(VisitIdentifier(node.Name), isActive:=True)
                Else
                    Return BadDirectiveTrivia(MissingToken(CS.SyntaxKind.HashToken).WithTrailingTrivia(TriviaList(Comment("/* " & node.ToString() & " */"))), isActive:=True)

                    Throw New NotSupportedException("Non-boolean directive constants.")
                End If

            End Function

            Public Overrides Function VisitConstructorStatement(node As VB.ConstructorStatementSyntax) As CS.SyntaxNode

                Dim typeName = CType(node.Parent.Parent, VB.TypeBlockSyntax).Begin.Identifier

                Dim subNewBlock As VB.MethodBlockSyntax = node.Parent

                Dim initializer As CS.ConstructorInitializerSyntax

                ' Check for chained constructor call.
                If subNewBlock.Statements.Count >= 1 Then
                    Dim constructorCall = TryCast(subNewBlock.Statements(0), VB.CallStatementSyntax)
                    If constructorCall IsNot Nothing Then

                        Dim invoke = TryCast(constructorCall.Invocation, VB.InvocationExpressionSyntax)
                        If invoke IsNot Nothing Then

                            Dim memberAccess = TryCast(invoke.Expression, VB.MemberAccessExpressionSyntax)
                            If memberAccess IsNot Nothing Then

                                If TypeOf memberAccess.Expression Is VB.InstanceExpressionSyntax AndAlso
                                   memberAccess.Name.Identifier.ToString().Equals("New", StringComparison.OrdinalIgnoreCase) Then

                                    Select Case memberAccess.Expression.Kind
                                        Case VB.SyntaxKind.MeExpression, VB.SyntaxKind.MyClassExpression
                                            initializer = ConstructorInitializer(CS.SyntaxKind.ThisConstructorInitializer, VisitArgumentList(invoke.ArgumentList))
                                        Case VB.SyntaxKind.MyBaseExpression
                                            initializer = ConstructorInitializer(CS.SyntaxKind.BaseConstructorInitializer, VisitArgumentList(invoke.ArgumentList))
                                    End Select
                                End If
                            End If
                        End If
                    End If
                End If

                ' TODO: Fix trivia transfer so that trailing trivia on this node doesn't end up on the close curly.
                ' TODO: Implement trivia transfer so that trivia on the Me.New or MyBase.New call is not lost.
                Return TransferTrivia(node, ConstructorDeclaration(VisitIdentifier(typeName)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithParameterList(VisitParameterList(node.ParameterList)) _
                                                .WithInitializer(initializer) _
                                                .WithBody(Block(List(Visit(If(initializer Is Nothing, subNewBlock.Statements, subNewBlock.Statements.Skip(1))))))
                                                )

            End Function

            Public Overrides Function VisitContinueStatement(node As VB.ContinueStatementSyntax) As CS.SyntaxNode

                ' So long as this continue statement binds to its immediately enclosing loop this is simple.
                ' Otherwise it would require rewriting with goto statements.
                ' TODO: Consider implementing this using binding instead.
                Dim parent = node.Parent
                Do Until VB.SyntaxFacts.IsDoLoopBlock(parent.Kind) OrElse
                         VB.SyntaxFacts.IsForBlock(parent.Kind) OrElse
                         parent.Kind = VB.SyntaxKind.WhileBlock

                    parent = parent.Parent
                Loop

                If (node.Kind = VB.SyntaxKind.ContinueDoStatement AndAlso VB.SyntaxFacts.IsDoLoopBlock(parent.Kind)) OrElse
                   (node.Kind = VB.SyntaxKind.ContinueForStatement AndAlso VB.SyntaxFacts.IsForBlock(parent.Kind)) OrElse
                   (node.Kind = VB.SyntaxKind.ContinueWhileStatement AndAlso parent.Kind = VB.SyntaxKind.WhileBlock) Then

                    Return ContinueStatement()
                Else

                    Return NotImplementedStatement(node)

                    Throw New NotImplementedException("Rewriting Continue statements which branch out of their immediately containing loop block into gotos.")
                End If

            End Function

            Public Overrides Function VisitDeclareStatement(node As VB.DeclareStatementSyntax) As CS.SyntaxNode
                ' Declare Ansi|Unicode|Auto Sub|Function Name Lib "LibName" Alias "AliasName"(ParameterList)[As ReturnType]
                ' [DllImport("LibName", CharSet: CharSet.Ansi|Unicode|Auto, EntryPoint: AliasName|Name)]
                ' extern ReturnType|void Name(ParameterList);

                Dim charSet As CS.ExpressionSyntax
                If node.CharsetKeyword.Kind = VB.SyntaxKind.None Then
                    charSet = SystemRuntimeInteropServicesCharSetAutoExpression
                Else
                    Select Case node.CharsetKeyword.Kind
                        Case VB.SyntaxKind.AnsiKeyword
                            charSet = SystemRuntimeInteropServicesCharSetAnsiExpression
                        Case VB.SyntaxKind.UnicodeKeyword
                            charSet = SystemRuntimeInteropServicesCharSetUnicodeExpression
                        Case VB.SyntaxKind.AutoKeyword
                            charSet = SystemRuntimeInteropServicesCharSetAutoExpression
                    End Select
                End If

                Dim aliasString As String
                If node.AliasKeyword.Kind = VB.SyntaxKind.None Then
                    aliasString = node.Identifier.ValueText
                Else
                    aliasString = node.AliasName.Token.ValueText
                End If


                Dim dllImportAttribute = Attribute(
                                             ParseName("global::System.Runtime.InteropServices.DllImport"),
                                             AttributeArgumentList({AttributeArgument(LiteralExpression(CS.SyntaxKind.StringLiteralExpression, Literal(node.LibraryName.Token.ToString(), node.LibraryName.Token.ValueText))),
                                                                      AttributeArgument(charSet).WithNameColon(NameColon(IdentifierName("CharSet"))),
                                                                      AttributeArgument(
                                                                          LiteralExpression(CS.SyntaxKind.StringLiteralExpression, Literal("""" & aliasString & """", aliasString))).WithNameColon(NameColon(IdentifierName("EntryPoint")))
                                                                     }.ToCommaSeparatedList()
                                             )
                                         )

                ' TODO: Transfer attributes on the return type to the statement.
                Return MethodDeclaration(DeriveType(node.Identifier, node.AsClause, node.Keyword), VisitIdentifier(node.Identifier)) _
                            .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists).Union({AttributeList({dllImportAttribute}.ToCommaSeparatedList())}))) _
                            .WithModifiers(TokenList(VisitModifiers(node.Modifiers).Union({Token(CS.SyntaxKind.ExternKeyword)}))) _
                            .WithParameterList(VisitParameterList(node.ParameterList))

            End Function

            Public Overrides Function VisitDelegateStatement(node As VB.DelegateStatementSyntax) As CS.SyntaxNode

                Return DelegateDeclaration(
                           DeriveType(node.Identifier, node.AsClause, node.Keyword),
                           VisitIdentifier(node.Identifier)) _
                       .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                       .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                       .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                       .WithParameterList(VisitParameterList(node.ParameterList)) _
                       .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList)))

            End Function

            Public Overrides Function VisitDirectiveTrivia(node As VB.DirectiveTriviaSyntax) As CS.SyntaxNode

                Return Visit(node.Directive)

            End Function

            Public Overrides Function VisitDocumentationCommentTrivia(node As VB.DocumentationCommentTriviaSyntax) As CS.SyntaxNode
                Return DocumentationCommentTrivia().WithEndOfComment(MissingToken(CS.SyntaxKind.EndOfDocumentationCommentToken).WithLeadingTrivia(TriviaList(Comment("/* " & node.ToString() & " */"))))
            End Function

            Public Overrides Function VisitDoLoopBlock(node As VB.DoLoopBlockSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.DoLoopTopTestBlock

                        Return WhileStatement(VisitWhileUntilClause(node.Begin.WhileUntilClause), Block(List(Visit(node.Statements))))

                    Case VB.SyntaxKind.DoLoopBottomTestBlock

                        Return DoStatement(Block(List(Visit(node.Statements))), VisitWhileUntilClause(node.End.WhileUntilClause))

                    Case VB.SyntaxKind.DoLoopForeverBlock

                        Return WhileStatement(LiteralExpression(CS.SyntaxKind.TrueLiteralExpression), Block(List(Visit(node.Statements))))

                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitDoStatement(node As VB.DoStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitElseDirective(node As VB.ElseDirectiveSyntax) As CS.SyntaxNode

                Return ElseDirectiveTrivia(isActive:=True, branchTaken:=False)

            End Function

            Public Overrides Function VisitElsePart(node As VB.ElsePartSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitElseStatement(node As VB.ElseStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitEmptyStatement(node As VB.EmptyStatementSyntax) As CS.SyntaxNode

                Return TransferTrivia(node, EmptyStatement())

            End Function

            Public Overrides Function VisitEndBlockStatement(node As VB.EndBlockStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitEndExternalSourceDirective(node As VB.EndExternalSourceDirective) As CS.SyntaxNode

                Return LineDirectiveTrivia(MissingToken(CS.SyntaxKind.NumericLiteralToken), isActive:=False)

            End Function

            Public Overrides Function VisitEndIfDirective(node As VB.EndIfDirectiveSyntax) As CS.SyntaxNode

                Return EndIfDirectiveTrivia(isActive:=False)

            End Function

            Public Overrides Function VisitEndRegionDirective(node As VB.EndRegionDirectiveSyntax) As CS.SyntaxNode

                Return EndRegionDirectiveTrivia(isActive:=False)

            End Function

            Public Overrides Function VisitEnumBlock(node As VB.EnumBlockSyntax) As CS.SyntaxNode

                Return VisitEnumStatement(node.Begin)

            End Function

            Public Overrides Function VisitEnumMemberDeclaration(node As VB.EnumMemberDeclarationSyntax) As CS.SyntaxNode

                Return TransferTrivia(node, EnumMemberDeclaration(List(VisitAttributeLists(node.AttributeLists)), VisitIdentifier(node.Identifier), VisitEqualsValue(node.Initializer)))

            End Function

            Public Overrides Function VisitEnumStatement(node As VB.EnumStatementSyntax) As CS.SyntaxNode

                Dim enumBlock As VB.EnumBlockSyntax = node.Parent

                Dim base As CS.BaseListSyntax
                If node.UnderlyingType IsNot Nothing Then
                    base = BaseList({VisitSimpleAsClause(node.UnderlyingType)}.ToCommaSeparatedList(Of CS.TypeSyntax))
                End If

                Return TransferTrivia(enumBlock, EnumDeclaration(VisitIdentifier(node.Identifier)) _
                                                    .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                    .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                    .WithBaseList(base) _
                                                    .WithMembers(Visit(enumBlock.Members).ToCommaSeparatedList(Of CS.EnumMemberDeclarationSyntax)())
                                                 )

            End Function

            Public Overrides Function VisitEqualsValue(node As VB.EqualsValueSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return EqualsValueClause(Visit(node.Value))

            End Function

            Public Overrides Function VisitEraseStatement(node As VB.EraseStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)

                ' TODO: Implement rewrite to call Array.Clear.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitErrorStatement(node As VB.ErrorStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)
                Throw New NotSupportedException(node.Kind.ToString())

            End Function

            Public Overrides Function VisitEventBlock(node As VB.EventBlockSyntax) As CS.SyntaxNode

                Return VisitEventStatement(node.Begin)

            End Function

            Public Overrides Function VisitEventStatement(node As VB.EventStatementSyntax) As CS.SyntaxNode

                Dim eventBlock = TryCast(node.Parent, VB.EventBlockSyntax)

                Dim accessors = If(eventBlock Is Nothing,
                                   Nothing,
                                   eventBlock.Accessors
                                )

                If node.AsClause IsNot Nothing Then
                    If accessors.Count = 0 Then
                        ' TODO: Synthesize an explicit interface implementation if this event's name differs from the name of the method in its Implements clause.
                        Return TransferTrivia(node, EventFieldDeclaration(
                                                        VariableDeclaration(
                                                            VisitSimpleAsClause(node.AsClause),
                                                            {VariableDeclarator(VisitIdentifier(node.Identifier))}.ToCommaSeparatedList()
                                                        )).WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                          .WithModifiers(TokenList(VisitModifiers(node.Modifiers)))
                                                    )

                    Else
                        Return NotImplementedMember(node)

                        Return TransferTrivia(eventBlock, EventDeclaration(
                                                              VisitSimpleAsClause(node.AsClause),
                                                              VisitIdentifier(node.Identifier)) _
                                                            .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                            .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                            .WithAccessorList(AccessorList(List(Visit(eventBlock.Accessors))))
                                                        )
                    End If
                Else
                    Return NotImplementedMember(node)

                    ' TODO: Implement rewrite to add implicit delegate declaration.
                    Throw New NotSupportedException("Events with inline parameter lists.")
                End If

            End Function

            Public Overrides Function VisitExitStatement(node As VB.ExitStatementSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.ExitSubStatement,
                         VB.SyntaxKind.ExitOperatorStatement

                        Return ReturnStatement()

                    Case VB.SyntaxKind.ExitTryStatement

                        Return NotImplementedStatement(node)
                        ' TODO: Implement a rewrite of this to a goto statement.
                        Throw New NotSupportedException(node.Kind.ToString())

                    Case VB.SyntaxKind.ExitSelectStatement

                        ' TODO: Implement rewrite to goto statement here if there are intermediate loops between this statement and the Select block.
                        Return BreakStatement()

                    Case VB.SyntaxKind.ExitPropertyStatement

                        Dim parent = node.Parent
                        Do Until TypeOf parent Is VB.MethodBaseSyntax
                            parent = parent.Parent
                        Loop

                        If parent.Kind = VB.SyntaxKind.PropertySetBlock Then
                            Return ReturnStatement()
                        Else
                            Return NotImplementedStatement(node)
                            ' TODO: Implement rewrite of Exit Property statements to return the implicit return variable.
                            Throw New NotSupportedException("Exit Property in a Property Get block.")
                        End If

                    Case VB.SyntaxKind.ExitFunctionStatement
                        Return NotImplementedStatement(node)

                        ' TODO: Implement rewrite of Exit Function statements to return the implicit return variable.
                        Throw New NotSupportedException("Exit Function statements.")

                    Case VB.SyntaxKind.ExitDoStatement,
                         VB.SyntaxKind.ExitForStatement,
                         VB.SyntaxKind.ExitWhileStatement

                        ' So long as this exit statement binds to its immediately enclosing block this is simple.
                        ' Otherwise it would require rewriting with goto statements.
                        ' TODO: Consider implementing this using binding instead.
                        Dim parent = node.Parent
                        Do Until VB.SyntaxFacts.IsDoLoopBlock(parent.Kind) OrElse
                                 VB.SyntaxFacts.IsForBlock(parent.Kind) OrElse
                                 parent.Kind = VB.SyntaxKind.WhileBlock

                            parent = parent.Parent
                        Loop

                        If (node.Kind = VB.SyntaxKind.ExitDoStatement AndAlso VB.SyntaxFacts.IsDoLoopBlock(parent.Kind)) OrElse
                           (node.Kind = VB.SyntaxKind.ExitForStatement AndAlso VB.SyntaxFacts.IsForBlock(parent.Kind)) OrElse
                           (node.Kind = VB.SyntaxKind.ExitWhileStatement AndAlso parent.Kind = VB.SyntaxKind.WhileBlock) Then

                            Return ContinueStatement()
                        Else

                            Return NotImplementedStatement(node)

                            Throw New NotImplementedException("Rewriting Exit statements which branch out of their immediately containing loop block into gotos.")
                        End If

                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitExternalChecksumDirective(node As VB.ExternalChecksumDirectiveSyntax) As CS.SyntaxNode

                Throw New NotSupportedException(node.Kind.ToString())

            End Function

            Public Overrides Function VisitExternalSourceDirective(node As VB.ExternalSourceDirectiveSyntax) As CS.SyntaxNode

                Return LineDirectiveTrivia(Literal(node.LineStart.ToString(), CInt(node.LineStart.Value)), isActive:=True) _
                            .WithFile(Literal(node.ExternalSource.ToString(), node.ExternalSource.ValueText))

            End Function

            Public Overrides Function VisitFieldDeclaration(node As VB.FieldDeclarationSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitFinallyPart(node As VB.FinallyPartSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return FinallyClause(Block(List(Visit(node.Statements))))

            End Function

            Public Overrides Function VisitFinallyStatement(node As VB.FinallyStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitForBlock(node As VB.ForBlockSyntax) As CS.SyntaxNode

                Return Visit(node.Begin)

            End Function

            Public Overrides Function VisitForEachStatement(node As VB.ForEachStatementSyntax) As CS.SyntaxNode

                Dim forBlock As VB.ForBlockSyntax = node.Parent

                Dim type As CS.TypeSyntax
                Dim identifier As CS.SyntaxToken

                Select Case node.ControlVariable.Kind
                    Case VB.SyntaxKind.IdentifierName

                        type = IdentifierName("var")
                        identifier = VisitIdentifier(CType(node.ControlVariable, VB.IdentifierNameSyntax).Identifier)

                    Case VB.SyntaxKind.VariableDeclarator

                        Dim declarator As VB.VariableDeclaratorSyntax = node.ControlVariable

                        type = DeriveType(declarator.Names(0), declarator.AsClause, declarator.Initializer)
                        identifier = VisitIdentifier(declarator.Names(0).Identifier)

                    Case Else

                        Return NotImplementedStatement(node)

                        Throw New NotSupportedException(node.ControlVariable.Kind.ToString())
                End Select

                Return TransferTrivia(forBlock, ForEachStatement(
                                                    type,
                                                    identifier,
                                                    Visit(node.Expression),
                                                    Block(List(Visit(forBlock.Statements)))
                                                )
                       )

            End Function

            Public Overrides Function VisitForStatement(node As VB.ForStatementSyntax) As CS.SyntaxNode

                Dim forBlock As VB.ForBlockSyntax = node.Parent

                Dim type As CS.TypeSyntax
                Dim identifier As CS.SyntaxToken

                Select Case node.ControlVariable.Kind
                    Case VB.SyntaxKind.IdentifierName

                        ' TODO: Bind to make sure this name isn't referencing an existing variable.
                        '       If it is then we shouldn't create a var declarator but instead an
                        '       initialization expression.
                        type = IdentifierName("var")
                        identifier = VisitIdentifier(CType(node.ControlVariable, VB.IdentifierNameSyntax).Identifier)

                    Case VB.SyntaxKind.VariableDeclarator

                        Dim declarator As VB.VariableDeclaratorSyntax = node.ControlVariable

                        type = DeriveType(declarator.Names(0), declarator.AsClause, declarator.Initializer)
                        identifier = VisitIdentifier(declarator.Names(0).Identifier)

                    Case Else

                        Return NotImplementedStatement(node)

                        Throw New NotSupportedException(node.ControlVariable.Kind.ToString())
                End Select

                Dim toValue = node.ToValue
                If toValue.Kind = VB.SyntaxKind.ParenthesizedExpression Then
                    toValue = CType(toValue, VB.ParenthesizedExpressionSyntax).Expression
                End If

                Dim declarationOpt = VariableDeclaration(type, {VariableDeclarator(identifier).WithInitializer(EqualsValueClause(Visit(node.FromValue)))}.ToCommaSeparatedList())

                Dim conditionOpt As CS.ExpressionSyntax = BinaryExpression(CS.SyntaxKind.LessThanOrEqualExpression, IdentifierName(identifier), Visit(toValue))

                Dim incrementor As CS.ExpressionSyntax = PostfixUnaryExpression(CS.SyntaxKind.PostIncrementExpression, IdentifierName(identifier))

                ' Rewrite ... To Count - 1 to < Count.
                If node.StepClause Is Nothing Then
                    If toValue.Kind = VB.SyntaxKind.SubtractExpression Then
                        Dim subtract As VB.BinaryExpressionSyntax = toValue

                        If subtract.Right.Kind = VB.SyntaxKind.NumericLiteralExpression AndAlso
                           CInt(CType(subtract.Right, VB.LiteralExpressionSyntax).Token.Value) = 1 Then

                            conditionOpt = BinaryExpression(CS.SyntaxKind.LessThanExpression, IdentifierName(identifier), Visit(subtract.Left))

                        End If
                    End If
                Else

                    Dim stepValue = node.StepClause.StepValue
                    If stepValue.Kind = VB.SyntaxKind.ParenthesizedExpression Then
                        stepValue = CType(stepValue, VB.ParenthesizedExpressionSyntax).Expression
                    End If

                    incrementor = BinaryExpression(CS.SyntaxKind.AddAssignExpression, IdentifierName(identifier), Visit(stepValue))

                    If stepValue.Kind = VB.SyntaxKind.NegateExpression Then
                        Dim negate As VB.UnaryExpressionSyntax = stepValue

                        conditionOpt = BinaryExpression(CS.SyntaxKind.GreaterThanOrEqualExpression, IdentifierName(identifier), Visit(toValue))

                        If negate.Operand.Kind = VB.SyntaxKind.NumericLiteralExpression AndAlso
                           CInt(CType(negate.Operand, VB.LiteralExpressionSyntax).Token.Value) = 1 Then

                            incrementor = PostfixUnaryExpression(CS.SyntaxKind.PostDecrementExpression, IdentifierName(identifier))
                        Else
                            incrementor = BinaryExpression(CS.SyntaxKind.SubtractAssignExpression, IdentifierName(identifier), Visit(negate.Operand))
                        End If
                    End If
                End If

                Return TransferTrivia(forBlock, ForStatement(Block(List(Visit(forBlock.Statements)))).WithDeclaration(declarationOpt).WithCondition(conditionOpt).WithIncrementors({incrementor}.ToCommaSeparatedList()))

            End Function

            Public Overrides Function VisitForStepClause(node As VB.ForStepClauseSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitGenericName(node As VB.GenericNameSyntax) As CS.SyntaxNode

                Return GenericName(VisitIdentifier(node.Identifier), VisitTypeArgumentList(node.TypeArgumentList))

            End Function

            Public Overrides Function VisitGetTypeExpression(node As VB.GetTypeExpressionSyntax) As CS.SyntaxNode

                Return TypeOfExpression(Visit(node.Type))

            End Function

            Public Overrides Function VisitGetXmlNamespaceExpression(node As VB.GetXmlNamespaceExpressionSyntax) As CS.SyntaxNode
                Return NotImplementedExpression(node)
            End Function

            Public Overrides Function VisitGlobalName(node As VB.GlobalNameSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitGoToStatement(node As VB.GoToStatementSyntax) As CS.SyntaxNode

                If node.Label.Kind = VB.SyntaxKind.IdentifierLabel Then
                    Return TransferTrivia(node, GotoStatement(CS.SyntaxKind.GotoStatement, IdentifierName(VisitIdentifier(node.Label.LabelToken))))
                Else
                    Return NotImplementedStatement(node)
                    ' Rewrite this label with an alpha prefix.
                    Throw New NotSupportedException("Goto statements with numeric label names.")
                End If

            End Function

            Public Overrides Function VisitGroupAggregation(node As VB.GroupAggregationSyntax) As CS.SyntaxNode
                Return MyBase.VisitGroupAggregation(node)
            End Function

            Public Overrides Function VisitGroupByClause(node As VB.GroupByClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitGroupByClause(node)
            End Function

            Public Overrides Function VisitGroupJoinClause(node As VB.GroupJoinClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitGroupJoinClause(node)
            End Function

            Public Overrides Function VisitHandlesClause(node As VB.HandlesClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitHandlesClause(node)
            End Function

            Public Overrides Function VisitHandlesClauseItem(node As VB.HandlesClauseItemSyntax) As CS.SyntaxNode
                Return MyBase.VisitHandlesClauseItem(node)
            End Function

            Public Overrides Function VisitIdentifierName(node As VB.IdentifierNameSyntax) As CS.SyntaxNode

                Return IdentifierName(VisitIdentifier(node.Identifier))

            End Function

            Protected Function VisitIdentifier(token As VB.SyntaxToken) As CS.SyntaxToken

                If token.Kind = VB.SyntaxKind.None Then Return Nothing

                Dim text = token.ValueText

                ' Strip out type characters.
                If Not Char.IsLetterOrDigit(text(text.Length - 1)) OrElse text.EndsWith("_") Then
                    text = text.Substring(0, text.Length - 1)
                End If

                If text = "_" Then
                    Return Identifier("_" & text)
                Else
                    Return Identifier(text)
                End If

            End Function

            Public Overrides Function VisitIfDirective(node As VB.IfDirectiveSyntax) As CS.SyntaxNode

                Return IfDirectiveTrivia(Visit(node.Condition), isActive:=False, branchTaken:=False, conditionValue:=False)

            End Function

            Public Overrides Function VisitIfPart(node As VB.IfPartSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitIfStatement(node As VB.IfStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitImplementsClause(node As VB.ImplementsClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitImplementsClause(node)
            End Function

            Public Overrides Function VisitImplementsStatement(node As VB.ImplementsStatementSyntax) As CS.SyntaxNode

                If node.Types.Count = 1 Then
                    Return Visit(node.Types(0))
                Else
                    Throw New InvalidOperationException()
                End If

            End Function

            Protected Function VisitImplementsStatements(statements As IEnumerable(Of VB.ImplementsStatementSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return Visit((Aggregate statement In statements Into SelectMany(statement.Types)))

            End Function

            Public Overrides Function VisitImportsStatement(node As VB.ImportsStatementSyntax) As CS.SyntaxNode

                If node.ImportsClauses.Count > 1 Then
                    Throw New InvalidOperationException()
                End If

                Return Visit(node.ImportsClauses(0))

            End Function

            Protected Function VisitImportsStatements(statements As IEnumerable(Of VB.ImportsStatementSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return Visit((Aggregate statement In statements Into SelectMany(statement.ImportsClauses)))

            End Function

            Public Overrides Function VisitInferredFieldInitializer(node As VB.InferredFieldInitializerSyntax) As CS.SyntaxNode

                Return Visit(node.Expression)

            End Function

            Public Overrides Function VisitInheritsStatement(node As VB.InheritsStatementSyntax) As CS.SyntaxNode

                If node.Types.Count = 1 Then
                    Return Visit(node.Types(0))
                Else
                    Throw New InvalidOperationException()
                End If

            End Function

            Protected Function VisitInheritsStatements(statements As IEnumerable(Of VB.InheritsStatementSyntax)) As IEnumerable(Of CS.SyntaxNode)

                Return Visit((Aggregate statement In statements Into SelectMany(statement.Types)))

            End Function

            Protected Overridable Function VisitInstanceExpression(node As VB.InstanceExpressionSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.MeExpression
                        Return ThisExpression()
                    Case VB.SyntaxKind.MyBaseExpression
                        Return BaseExpression()
                    Case VB.SyntaxKind.MyClassExpression
                        Return NotImplementedExpression(node)
                        Throw New NotSupportedException("C# doesn't have a MyClass equivalent")
                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitMeExpression(node As Roslyn.Compilers.VisualBasic.MeExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return VisitInstanceExpression(node)
            End Function

            Public Overrides Function VisitMyBaseExpression(node As Roslyn.Compilers.VisualBasic.MyBaseExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return VisitInstanceExpression(node)
            End Function

            Public Overrides Function VisitMyClassExpression(node As Roslyn.Compilers.VisualBasic.MyClassExpressionSyntax) As Roslyn.Compilers.CSharp.SyntaxNode
                Return VisitInstanceExpression(node)
            End Function

            Public Overrides Function VisitInvocationExpression(node As VB.InvocationExpressionSyntax) As CS.SyntaxNode

                ' TODO: Use binding to detect whether this is an invocation or an index, 
                '       and if an index whether off a property or the result of an implicit method invocation.
                Return InvocationExpression(Visit(node.Expression), VisitArgumentList(node.ArgumentList))

            End Function

            Public Overrides Function VisitJoinCondition(node As VB.JoinConditionSyntax) As CS.SyntaxNode
                Return MyBase.VisitJoinCondition(node)
            End Function

            Public Overrides Function VisitJoinClause(node As VB.JoinClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitJoinClause(node)
            End Function

            Public Overrides Function VisitLabelStatement(node As VB.LabelStatementSyntax) As CS.SyntaxNode

                Return LabeledStatement(VisitIdentifier(node.LabelToken), EmptyStatement())

            End Function

            Public Overrides Function VisitLambdaHeader(node As VB.LambdaHeaderSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitLiteralExpression(node As VB.LiteralExpressionSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.StringLiteralExpression

                        ' VB String literals are effectively implicitly escaped (a.k.a verbatim).
                        Dim valueText = If(node.Token.ValueText.Contains("\"),
                                           "@" & """" & node.Token.ValueText & """",
                                           """" & node.Token.ValueText & """"
                                        )

                        Return LiteralExpression(CS.SyntaxKind.StringLiteralExpression, Literal(valueText, CStr(node.Token.Value)))

                    Case VB.SyntaxKind.CharacterLiteralExpression

                        Return LiteralExpression(CS.SyntaxKind.StringLiteralExpression, Literal("'" & node.Token.ValueText & "'", CChar(node.Token.Value)))

                    Case VB.SyntaxKind.TrueLiteralExpression

                        Return LiteralExpression(CS.SyntaxKind.TrueLiteralExpression)

                    Case VB.SyntaxKind.FalseLiteralExpression

                        Return LiteralExpression(CS.SyntaxKind.FalseLiteralExpression)

                    Case VB.SyntaxKind.DateLiteralExpression

                        Return NotImplementedExpression(node)

                        ' TODO: Rewrite to new global::System.DateTime.Parse("yyyy-MM-dd HH:mm:ss")
                        Throw New NotImplementedException(node.ToString())

                    Case VB.SyntaxKind.NumericLiteralExpression

                        Select Case node.Token.Kind

                            Case VB.SyntaxKind.DecimalLiteralToken

                                Return LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, Literal(node.Token.ValueText, CDec(node.Token.Value)))

                            Case VB.SyntaxKind.FloatingLiteralToken

                                Return LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, Literal(node.Token.ValueText, CDbl(node.Token.Value)))

                            Case VB.SyntaxKind.IntegerLiteralToken

                                Dim literalText As String

                                Select Case node.Token.Base
                                    Case VB.LiteralBase.Decimal
                                        literalText = node.Token.ValueText

                                    Case VB.LiteralBase.Hexadecimal,
                                         VB.LiteralBase.Octal

                                        literalText = "0x" & CType(node.Token.Value, IFormattable).ToString("X02", formatProvider:=Nothing)

                                End Select

                                Dim literalToken As CS.SyntaxToken

                                Select Case node.Token.TypeCharacter
                                    Case VB.TypeCharacter.ShortLiteral

                                        literalToken = Literal(literalText, CShort(node.Token.Value))

                                    Case VB.TypeCharacter.IntegerLiteral

                                        literalToken = Literal(literalText, CInt(node.Token.Value))

                                    Case VB.TypeCharacter.LongLiteral

                                        literalToken = Literal(literalText, CLng(node.Token.Value))

                                    Case VB.TypeCharacter.UShortLiteral

                                        literalToken = Literal(literalText, CUShort(node.Token.Value))

                                    Case VB.TypeCharacter.UIntegerLiteral

                                        literalToken = Literal(literalText, CUInt(node.Token.Value))

                                    Case VB.TypeCharacter.ULongLiteral

                                        literalToken = Literal(literalText, CULng(node.Token.Value))

                                    Case Else ' Default to Integer type

                                        literalToken = Literal(literalText, CInt(node.Token.Value))

                                End Select

                                Return LiteralExpression(CS.SyntaxKind.NumericLiteralExpression, literalToken)

                            Case Else
                                Return NotImplementedExpression(node)

                                Throw New NotSupportedException(node.Token.Kind.ToString())
                        End Select

                    Case VB.SyntaxKind.NothingLiteralExpression
                        ' TODO: Bind this expression in context to determine whether this translates to null or default(T).
                        Return LiteralExpression(CS.SyntaxKind.NullLiteralExpression)

                    Case Else
                        Return NotImplementedExpression(node)

                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitLocalDeclarationStatement(node As VB.LocalDeclarationStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitLoopStatement(node As VB.LoopStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitMemberAccessExpression(node As VB.MemberAccessExpressionSyntax) As CS.SyntaxNode

                If node.Expression Is Nothing Then

                    Return NotImplementedExpression(node)
                    ' TODO: Rewrite WithBlock member access.
                    Throw New NotImplementedException(node.ToString())
                End If

                Select Case node.Kind

                    Case VB.SyntaxKind.MemberAccessExpression

                        Return MemberAccessExpression(CS.SyntaxKind.MemberAccessExpression, Visit(node.Expression), Visit(node.Name))

                    Case VB.SyntaxKind.DictionaryAccessExpression

                        Return NotImplementedExpression(node)
                        ' TODO: Rewrite to Invocation.
                        Throw New NotImplementedException(node.ToString())

                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitMembersImportsClause(node As VB.MembersImportsClauseSyntax) As CS.SyntaxNode

                Return TransferTrivia(node.Parent, UsingDirective(Visit(node.Name)))

            End Function

            Protected Function VisitMembers(statements As IEnumerable(Of VB.StatementSyntax)) As IEnumerable(Of CS.MemberDeclarationSyntax)
                Dim members As New List(Of CS.MemberDeclarationSyntax)

                For Each statement In statements
                    Dim converted = Visit(statement)

                    If TypeOf converted Is CS.MemberDeclarationSyntax Then
                        members.Add(converted)
                    ElseIf TypeOf converted Is CS.StatementSyntax Then
                        members.Add(GlobalStatement(converted))
                    Else
                        Throw New NotSupportedException(converted.Kind.ToString())
                    End If
                Next

                Return members
            End Function

            Public Overrides Function VisitMethodBlock(node As VB.MethodBlockSyntax) As CS.SyntaxNode

                Return Visit(node.Begin)

            End Function

            Public Overrides Function VisitMethodStatement(node As VB.MethodStatementSyntax) As CS.SyntaxNode

                ' A MustInherit method, or a method inside an Interface definition will be directly parented by the TypeBlock.
                Dim methodBlock = TryCast(node.Parent, VB.MethodBlockSyntax)

                Dim triviaSource As VB.SyntaxNode = node
                If methodBlock IsNot Nothing Then
                    triviaSource = methodBlock
                End If

                Return TransferTrivia(triviaSource, MethodDeclaration(
                                                        DeriveType(node.Identifier, node.AsClause, node.Keyword),
                                                        VisitIdentifier(node.Identifier)) _
                                                    .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                    .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                    .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                                                    .WithParameterList(VisitParameterList(node.ParameterList)) _
                                                    .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList))) _
                                                    .WithBody(If(methodBlock Is Nothing, Nothing, Block(List(Visit(methodBlock.Statements))))) _
                                                    .WithSemicolonToken(If(methodBlock Is Nothing, Token(CS.SyntaxKind.SemicolonToken), Nothing))
                                        )

            End Function

            Protected Function VisitModifier(token As VB.SyntaxToken) As CS.SyntaxToken

                Dim kind As CS.SyntaxKind

                Select Case token.Kind
                    Case VB.SyntaxKind.PublicKeyword
                        kind = CS.SyntaxKind.PublicKeyword
                    Case VB.SyntaxKind.PrivateKeyword
                        kind = CS.SyntaxKind.PrivateKeyword
                    Case VB.SyntaxKind.ProtectedKeyword
                        kind = CS.SyntaxKind.ProtectedKeyword
                    Case VB.SyntaxKind.FriendKeyword
                        kind = CS.SyntaxKind.InternalKeyword
                    Case VB.SyntaxKind.SharedKeyword
                        kind = CS.SyntaxKind.StaticKeyword
                    Case VB.SyntaxKind.OverridesKeyword
                        kind = CS.SyntaxKind.OverrideKeyword
                    Case VB.SyntaxKind.OverridableKeyword
                        kind = CS.SyntaxKind.VirtualKeyword
                    Case VB.SyntaxKind.MustOverrideKeyword
                        kind = CS.SyntaxKind.AbstractKeyword
                    Case VB.SyntaxKind.NotOverridableKeyword
                        kind = CS.SyntaxKind.SealedKeyword
                    Case VB.SyntaxKind.OverloadsKeyword
                        kind = CS.SyntaxKind.NewKeyword
                    Case VB.SyntaxKind.MustInheritKeyword
                        kind = CS.SyntaxKind.AbstractKeyword
                    Case VB.SyntaxKind.NotInheritableKeyword
                        kind = CS.SyntaxKind.SealedKeyword
                    Case VB.SyntaxKind.PartialKeyword
                        kind = CS.SyntaxKind.PartialKeyword
                    Case VB.SyntaxKind.ByRefKeyword
                        kind = CS.SyntaxKind.RefKeyword
                    Case VB.SyntaxKind.ParamArrayKeyword
                        kind = CS.SyntaxKind.ParamsKeyword
                    Case VB.SyntaxKind.NarrowingKeyword
                        kind = CS.SyntaxKind.ExplicitKeyword
                    Case VB.SyntaxKind.WideningKeyword
                        kind = CS.SyntaxKind.ImplicitKeyword
                    Case VB.SyntaxKind.ConstKeyword
                        kind = CS.SyntaxKind.ConstKeyword
                    Case VB.SyntaxKind.ReadOnlyKeyword

                        If TypeOf token.Parent Is VB.PropertyStatementSyntax Then
                            kind = CS.SyntaxKind.None
                        Else
                            kind = CS.SyntaxKind.ReadOnlyKeyword
                        End If

                    Case VB.SyntaxKind.DimKeyword
                        kind = CS.SyntaxKind.None
                    Case Else
                        Return NotImplementedModifier(token)

                        Throw New NotSupportedException(token.Kind.ToString())
                End Select

                Return CS.Syntax.Token(kind)

            End Function

            Protected Function VisitModifiers(tokens As IEnumerable(Of VB.SyntaxToken)) As IEnumerable(Of CS.SyntaxToken)

                Return From
                           token In tokens
                       Where
                           token.Kind <> VB.SyntaxKind.ByValKeyword AndAlso
                           token.Kind <> VB.SyntaxKind.OptionalKeyword
                       Select
                           translation = VisitModifier(token)
                       Where
                           translation.Kind <> CS.SyntaxKind.None

            End Function

            Public Overrides Function VisitModifiedIdentifier(node As VB.ModifiedIdentifierSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitMultiLineIfBlock(node As VB.MultiLineIfBlockSyntax) As CS.SyntaxNode

                Dim elseOpt As CS.ElseClauseSyntax

                ' TODO: Transfer trivia for each elseif/else block.
                If node.ElsePart IsNot Nothing Then
                    elseOpt = ElseClause(Block(List(Visit(node.ElsePart.Statements))))
                End If

                For i = node.ElseIfParts.Count - 1 To 0 Step -1
                    elseOpt = ElseClause(
                                  IfStatement(
                                      Visit(node.ElseIfParts(i).Begin.Condition),
                                      Block(List(Visit(node.ElseIfParts(i).Statements)))) _
                                    .WithElse(elseOpt)
                                  )
                Next

                Return TransferTrivia(node, IfStatement(
                                                Visit(node.IfPart.Begin.Condition),
                                                Block(List(Visit(node.IfPart.Statements)))) _
                                                .WithElse(elseOpt)
                                            )

            End Function

            Public Overrides Function VisitMultiLineLambdaExpression(node As VB.MultiLineLambdaExpressionSyntax) As CS.SyntaxNode

                Return ParenthesizedLambdaExpression(Block(List(Visit(node.Statements)))).WithParameterList(VisitParameterList(node.Begin.ParameterList))

            End Function

            Public Overrides Function VisitNamedArgument(node As VB.NamedArgumentSyntax) As CS.SyntaxNode

                If TypeOf node.Parent.Parent Is VB.AttributeSyntax Then
                    Return AttributeArgument(Visit(node.Expression)).WithNameColon(NameColon(IdentifierName(VisitIdentifier(node.IdentifierName.Identifier))))
                Else
                    ' TODO: Bind to discover ByRef arguments.
                    Return CS.Syntax.Argument(Visit(node.Expression)).WithNameColon(NameColon(IdentifierName(VisitIdentifier(node.IdentifierName.Identifier))))
                End If

            End Function

            Public Overrides Function VisitNamedFieldInitializer(node As VB.NamedFieldInitializerSyntax) As CS.SyntaxNode

                Return If(node.Parent.Parent.Kind = VB.SyntaxKind.AnonymousObjectCreationExpression,
                          CType(AnonymousObjectMemberDeclarator(NameEquals(VisitIdentifierName(node.Name)), Visit(node.Expression)), CS.SyntaxNode),
                          BinaryExpression(CS.SyntaxKind.AssignExpression, VisitIdentifierName(node.Name), Visit(node.Expression)))

            End Function

            Public Overrides Function VisitNamespaceBlock(node As VB.NamespaceBlockSyntax) As CS.SyntaxNode

                Return VisitNamespaceStatement(node.Begin)

            End Function

            Public Overrides Function VisitNamespaceStatement(node As VB.NamespaceStatementSyntax) As CS.SyntaxNode

                Dim namespaceBlock As VB.NamespaceBlockSyntax = node.Parent

                If node.Name.Kind = VB.SyntaxKind.GlobalName Then

                    ' TODO: Split all members to declare in global namespace.
                    Throw New NotImplementedException(node.ToString())

                Else
                    Dim baseName = node.Name
                    Do While TypeOf baseName Is VB.QualifiedNameSyntax
                        baseName = CType(baseName, VB.QualifiedNameSyntax).Left
                    Loop

                    Dim remainingNames = TryCast(baseName.Parent, VB.QualifiedNameSyntax)

                    Dim finalName As CS.NameSyntax

                    ' Strip out the Global name.
                    If baseName.Kind = VB.SyntaxKind.GlobalName Then
                        finalName = Visit(remainingNames.Right)
                        remainingNames = TryCast(remainingNames.Parent, VB.QualifiedNameSyntax)
                    ElseIf RootNamespaceName IsNot Nothing Then
                        finalName = QualifiedName(RootNamespaceName, Visit(baseName))
                    Else
                        finalName = Visit(baseName)
                    End If

                    Do Until remainingNames Is Nothing
                        finalName = QualifiedName(finalName, Visit(remainingNames.Right))
                        remainingNames = TryCast(remainingNames.Parent, VB.QualifiedNameSyntax)
                    Loop

                    Return TransferTrivia(node, NamespaceDeclaration(finalName).WithMembers(List(Visit(namespaceBlock.Members))))

                End If

            End Function

            Public Overrides Function VisitNextStatement(node As VB.NextStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitNullableType(node As VB.NullableTypeSyntax) As CS.SyntaxNode

                Return NullableType(Visit(node.ElementType))

            End Function

            Public Overrides Function VisitObjectCollectionInitializer(node As VB.ObjectCollectionInitializerSyntax) As CS.SyntaxNode

                ' TODO: Figure out what to do if the initializers contain nested initializers that invoke extension methods.
                Return VisitCollectionInitializer(node.Initializer)

            End Function

            Public Overrides Function VisitObjectCreationExpression(node As VB.ObjectCreationExpressionSyntax) As CS.SyntaxNode

                Return ObjectCreationExpression(Visit(node.Type)) _
                            .WithArgumentList(VisitArgumentList(node.ArgumentList)) _
                            .WithInitializer(Visit(node.Initializer))

            End Function

            Public Overrides Function VisitObjectMemberInitializer(node As VB.ObjectMemberInitializerSyntax) As CS.SyntaxNode

                Return InitializerExpression(CS.SyntaxKind.ObjectInitializerExpression, Visit(node.Initializers).ToCommaSeparatedList(Of CS.ExpressionSyntax))

            End Function

            Public Overrides Function VisitOmittedArgument(node As VB.OmittedArgumentSyntax) As CS.SyntaxNode

                Return CS.Syntax.Argument(NotImplementedExpression(node))

                ' TODO: Bind to discover default values.
                Throw New NotImplementedException(node.ToString())
            End Function

            Public Overrides Function VisitOnErrorGoToStatement(node As VB.OnErrorGoToStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)

            End Function

            Public Overrides Function VisitOnErrorResumeNextStatement(node As VB.OnErrorResumeNextStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)

            End Function

            Public Overrides Function VisitOperatorStatement(node As VB.OperatorStatementSyntax) As CS.SyntaxNode

                Dim operatorBlock As VB.MethodBlockSyntax = node.Parent

                Dim kind As CS.SyntaxKind
                Select Case node.Operator.Kind

                    Case VB.SyntaxKind.CTypeKeyword

                        Dim otherModifiers As New List(Of VB.SyntaxToken)(node.Modifiers.Count)
                        Dim implicitOrExplicitKeyword As CS.SyntaxToken

                        For Each modifier In node.Modifiers
                            Select Case modifier.Kind
                                Case VB.SyntaxKind.NarrowingKeyword
                                    implicitOrExplicitKeyword = Token(CS.SyntaxKind.ExplicitKeyword)
                                Case VB.SyntaxKind.WideningKeyword
                                    implicitOrExplicitKeyword = Token(CS.SyntaxKind.ImplicitKeyword)
                                Case Else
                                    otherModifiers.Add(modifier)
                            End Select
                        Next

                        Return TransferTrivia(operatorBlock, ConversionOperatorDeclaration(
                                                                implicitOrExplicitKeyword,
                                                                VisitSimpleAsClause(node.AsClause)) _
                                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                                .WithModifiers(TokenList(VisitModifiers(otherModifiers))) _
                                                                .WithParameterList(VisitParameterList(node.ParameterList)) _
                                                                .WithBody(Block(List(Visit(operatorBlock.Statements))))
                                                             )

                    Case VB.SyntaxKind.IsTrueKeyword
                        kind = CS.SyntaxKind.TrueKeyword
                    Case VB.SyntaxKind.IsFalseKeyword
                        kind = CS.SyntaxKind.FalseKeyword
                    Case VB.SyntaxKind.NotKeyword
                        kind = CS.SyntaxKind.BitwiseNotExpression
                    Case VB.SyntaxKind.PlusToken
                        kind = CS.SyntaxKind.PlusToken
                    Case VB.SyntaxKind.MinusToken
                        kind = CS.SyntaxKind.MinusMinusToken
                    Case VB.SyntaxKind.AsteriskToken
                        kind = CS.SyntaxKind.AsteriskToken
                    Case VB.SyntaxKind.SlashToken
                        kind = CS.SyntaxKind.SlashToken
                    Case VB.SyntaxKind.LessThanLessThanToken
                        kind = CS.SyntaxKind.LessThanLessThanToken
                    Case VB.SyntaxKind.GreaterThanGreaterThanToken
                        kind = CS.SyntaxKind.GreaterThanGreaterThanToken
                    Case VB.SyntaxKind.ModKeyword
                        kind = CS.SyntaxKind.PercentToken
                    Case VB.SyntaxKind.OrKeyword
                        kind = CS.SyntaxKind.BarToken
                    Case VB.SyntaxKind.XorKeyword
                        kind = CS.SyntaxKind.CaretToken
                    Case VB.SyntaxKind.AndKeyword
                        kind = CS.SyntaxKind.AmpersandToken
                    Case VB.SyntaxKind.EqualsToken
                        kind = CS.SyntaxKind.EqualsEqualsToken
                    Case VB.SyntaxKind.LessThanGreaterThanToken
                        kind = CS.SyntaxKind.ExclamationEqualsToken
                    Case VB.SyntaxKind.LessThanToken
                        kind = CS.SyntaxKind.LessThanToken
                    Case VB.SyntaxKind.LessThanEqualsToken
                        kind = CS.SyntaxKind.LessThanEqualsToken
                    Case VB.SyntaxKind.GreaterThanEqualsToken
                        kind = CS.SyntaxKind.GreaterThanEqualsToken
                    Case VB.SyntaxKind.GreaterThanToken
                        kind = CS.SyntaxKind.GreaterThanToken

                    Case VB.SyntaxKind.AmpersandToken,
                         VB.SyntaxKind.BackslashToken,
                         VB.SyntaxKind.LikeKeyword,
                         VB.SyntaxKind.CaretToken

                        Return NotImplementedMember(node)
                        ' TODO: Rewrite this as a normal method with the System.Runtime.CompilerServices.SpecialName attribute.
                        Throw New NotImplementedException(node.ToString())

                End Select

                Return TransferTrivia(operatorBlock, OperatorDeclaration(
                                                         DeriveType(node.Operator, node.AsClause, node.Keyword),
                                                         Token(kind)) _
                                                     .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                     .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                     .WithParameterList(VisitParameterList(node.ParameterList)) _
                                                     .WithBody(Block(List(Visit(operatorBlock.Statements))))
                                        )

            End Function

            Public Overrides Function VisitOptionStatement(node As VB.OptionStatementSyntax) As CS.SyntaxNode

                Select Case node.NameKeyword.Kind
                    Case VB.SyntaxKind.ExplicitKeyword
                        If node.ValueKeyword.Kind = VB.SyntaxKind.OffKeyword Then
                            IsOptionExplicitOn = False

                            ' TODO: Log this.
                            ''Throw New NotSupportedException("Option Explicit Off")
                        End If
                    Case VB.SyntaxKind.CompareKeyword
                        If node.ValueKeyword.Kind = VB.SyntaxKind.TextKeyword Then
                            IsOptionCompareBinary = False

                            ' TODO: Log this.
                            ''Throw New NotImplementedException("Option Compare Text")
                        End If
                    Case VB.SyntaxKind.StrictKeyword

                        IsOptionStrictOn = node.ValueKeyword.Kind <> VB.SyntaxKind.OffKeyword

                    Case VB.SyntaxKind.InferKeyword

                        IsOptionInferOn = node.ValueKeyword.Kind <> VB.SyntaxKind.OffKeyword

                End Select

                Return Nothing
            End Function

            Public Overrides Function VisitOrderByClause(node As VB.OrderByClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitOrderByClause(node)
            End Function

            Public Overrides Function VisitOrdering(node As VB.OrderingSyntax) As CS.SyntaxNode
                Return MyBase.VisitOrdering(node)
            End Function

            Public Overrides Function VisitParameter(node As VB.ParameterSyntax) As CS.SyntaxNode

                Return Parameter(
                           List(VisitAttributeLists(node.AttributeLists)),
                           TokenList(VisitModifiers(node.Modifiers)),
                           DeriveType(node.Identifier, node.AsClause, initializer:=Nothing),
                           VisitIdentifier(node.Identifier.Identifier),
                           VisitEqualsValue(node.Default)
                       )

            End Function

            Public Overrides Function VisitParameterList(node As VB.ParameterListSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return ParameterList()

                Return ParameterList(Visit(node.Parameters).ToCommaSeparatedList(Of CS.ParameterSyntax))

            End Function

            Public Overrides Function VisitParenthesizedExpression(node As VB.ParenthesizedExpressionSyntax) As CS.SyntaxNode

                Return ParenthesizedExpression(Visit(node.Expression))

            End Function

            Public Overrides Function VisitPartitionClause(node As VB.PartitionClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitPartitionClause(node)
            End Function

            Public Overrides Function VisitPartitionWhileClause(node As VB.PartitionWhileClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitPartitionWhileClause(node)
            End Function

            Public Overrides Function VisitPredefinedCastExpression(node As VB.PredefinedCastExpressionSyntax) As CS.SyntaxNode

                ' NOTE: For conversions between intrinsic types this is an over-simplification.
                '       Depending on the source and target types this may be a C# cast, a VB runtime call, a BCL call, or a simple IL instruction.
                Dim kind As CS.SyntaxKind

                Select Case node.Keyword.Kind
                    Case VB.SyntaxKind.CByteKeyword
                        kind = CS.SyntaxKind.ByteKeyword
                    Case VB.SyntaxKind.CUShortKeyword
                        kind = CS.SyntaxKind.UShortKeyword
                    Case VB.SyntaxKind.CUIntKeyword
                        kind = CS.SyntaxKind.UIntKeyword
                    Case VB.SyntaxKind.CULngKeyword
                        kind = CS.SyntaxKind.ULongKeyword
                    Case VB.SyntaxKind.CSByteKeyword
                        kind = CS.SyntaxKind.SByteKeyword
                    Case VB.SyntaxKind.CShortKeyword
                        kind = CS.SyntaxKind.ShortKeyword
                    Case VB.SyntaxKind.CIntKeyword
                        kind = CS.SyntaxKind.IntKeyword
                    Case VB.SyntaxKind.CLngKeyword
                        kind = CS.SyntaxKind.LongKeyword
                    Case VB.SyntaxKind.CSngKeyword
                        kind = CS.SyntaxKind.FloatKeyword
                    Case VB.SyntaxKind.CDblKeyword
                        kind = CS.SyntaxKind.DoubleKeyword
                    Case VB.SyntaxKind.CDecKeyword
                        kind = CS.SyntaxKind.DecimalKeyword
                    Case VB.SyntaxKind.CStrKeyword
                        kind = CS.SyntaxKind.StringKeyword
                    Case VB.SyntaxKind.CCharKeyword
                        kind = CS.SyntaxKind.CharKeyword
                    Case VB.SyntaxKind.CDateKeyword
                        Return ParenthesizedExpression(CastExpression(ParseTypeName("global::System.DateTime"), Visit(node.Expression)))
                    Case VB.SyntaxKind.CBoolKeyword
                        kind = CS.SyntaxKind.BoolKeyword
                    Case VB.SyntaxKind.CObjKeyword
                        kind = CS.SyntaxKind.ObjectKeyword
                    Case Else
                        Throw New NotSupportedException(node.Keyword.Kind.ToString())
                End Select

                Return ParenthesizedExpression(CastExpression(PredefinedType(Token(kind)), Visit(node.Expression)))

            End Function

            Public Overrides Function VisitPredefinedType(node As VB.PredefinedTypeSyntax) As CS.SyntaxNode

                Dim kind As CS.SyntaxKind

                Select Case node.Keyword.Kind
                    Case VB.SyntaxKind.ByteKeyword
                        kind = CS.SyntaxKind.ByteKeyword
                    Case VB.SyntaxKind.UShortKeyword
                        kind = CS.SyntaxKind.UShortKeyword
                    Case VB.SyntaxKind.UIntegerKeyword
                        kind = CS.SyntaxKind.UIntKeyword
                    Case VB.SyntaxKind.ULongKeyword
                        kind = CS.SyntaxKind.ULongKeyword
                    Case VB.SyntaxKind.SByteKeyword
                        kind = CS.SyntaxKind.SByteKeyword
                    Case VB.SyntaxKind.ShortKeyword
                        kind = CS.SyntaxKind.ShortKeyword
                    Case VB.SyntaxKind.IntegerKeyword
                        kind = CS.SyntaxKind.IntKeyword
                    Case VB.SyntaxKind.LongKeyword
                        kind = CS.SyntaxKind.LongKeyword
                    Case VB.SyntaxKind.SingleKeyword
                        kind = CS.SyntaxKind.FloatKeyword
                    Case VB.SyntaxKind.DoubleKeyword
                        kind = CS.SyntaxKind.DoubleKeyword
                    Case VB.SyntaxKind.DecimalKeyword
                        kind = CS.SyntaxKind.DecimalKeyword
                    Case VB.SyntaxKind.StringKeyword
                        kind = CS.SyntaxKind.StringKeyword
                    Case VB.SyntaxKind.CharKeyword
                        kind = CS.SyntaxKind.CharKeyword
                    Case VB.SyntaxKind.DateKeyword
                        Return ParseTypeName("global::System.DateTime")
                    Case VB.SyntaxKind.BooleanKeyword
                        kind = CS.SyntaxKind.BoolKeyword
                    Case VB.SyntaxKind.ObjectKeyword
                        kind = CS.SyntaxKind.ObjectKeyword
                    Case Else
                        Throw New NotSupportedException(node.Keyword.Kind.ToString())
                End Select

                Return PredefinedType(Token(kind))

            End Function

            Public Overrides Function VisitPropertyBlock(node As VB.PropertyBlockSyntax) As CS.SyntaxNode

                Return VisitPropertyStatement(node.Begin)

            End Function

            Public Overrides Function VisitPropertyStatement(node As VB.PropertyStatementSyntax) As CS.SyntaxNode

                Dim propertyBlockOpt = TryCast(node.Parent, VB.PropertyBlockSyntax)

                If propertyBlockOpt IsNot Nothing Then
                    Return TransferTrivia(propertyBlockOpt, PropertyDeclaration(
                                                                DeriveType(node.Identifier, node.AsClause),
                                                                VisitIdentifier(node.Identifier)) _
                                                            .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                            .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                            .WithAccessorList(AccessorList(List(Visit(propertyBlockOpt.Accessors))))
                                                    )
                Else

                    Dim accessors = New List(Of CS.AccessorDeclarationSyntax)() From {
                                            AccessorDeclaration(CS.SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SemicolonToken),
                                            AccessorDeclaration(CS.SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SemicolonToken)
                                        }

                    ' For MustOverride properties and properties in interfaces we have to check the modifiers
                    ' to determine whether get or set accessors should be generated.
                    For Each modifier In node.Modifiers
                        Select Case modifier.Kind
                            Case VB.SyntaxKind.ReadOnlyKeyword
                                accessors.RemoveAt(1)
                            Case (VB.SyntaxKind.WriteOnlyKeyword)
                                accessors.RemoveAt(0)
                        End Select
                    Next

                    ' TODO: Transfer initializers on the auto-prop to the constructor.
                    Return TransferTrivia(node, PropertyDeclaration(
                                                    DeriveType(node.Identifier, node.AsClause),
                                                    VisitIdentifier(node.Identifier)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithAccessorList(AccessorList(List(accessors)))
                                    )
                End If

            End Function

            Public Overrides Function VisitQualifiedName(node As VB.QualifiedNameSyntax) As CS.SyntaxNode

                If TypeOf node.Left Is VB.GlobalNameSyntax Then
                    Return AliasQualifiedName(IdentifierName("global"), Visit(node.Right))
                Else
                    Return QualifiedName(Visit(node.Left), Visit(node.Right))
                End If

            End Function

            Public Overrides Function VisitQueryExpression(node As VB.QueryExpressionSyntax) As CS.SyntaxNode

                Return (New QueryClauseConvertingVisitor(parent:=Me)).Visit(node)

            End Function

            Public Overrides Function VisitRaiseEventStatement(node As VB.RaiseEventStatementSyntax) As CS.SyntaxNode

                ' TODO: Rewrite to a conditional invocation based on a thread-safe null check.
                Return TransferTrivia(node, ExpressionStatement(InvocationExpression(VisitIdentifierName(node.Name), VisitArgumentList(node.ArgumentList))))

            End Function

            Public Overrides Function VisitRangeArgument(node As VB.RangeArgumentSyntax) As CS.SyntaxNode
                Return MyBase.VisitRangeArgument(node)
            End Function

            Public Overrides Function VisitReDimStatement(node As VB.ReDimStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)
                ' TODO: Implement rewrite to Array.CopyTo with new array creation.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitRegionDirective(node As VB.RegionDirectiveSyntax) As CS.SyntaxNode

                Return RegionDirectiveTrivia(isActive:=False).WithRegionKeyword(Token(CS.SyntaxKind.RegionKeyword, TriviaList(PreprocessingMessage(node.Name.ValueText))))

            End Function

            Public Overrides Function VisitResumeStatement(node As VB.ResumeStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)
                Throw New NotSupportedException("Resume statements.")

            End Function

            Public Overrides Function VisitReturnStatement(node As VB.ReturnStatementSyntax) As CS.SyntaxNode

                Return ReturnStatement(Visit(node.Expression))

            End Function

            Public Overrides Function VisitSelectBlock(node As VB.SelectBlockSyntax) As CS.SyntaxNode

                ' TODO: Bind to expression to ensure it's of a type C# can switch on.
                Return TransferTrivia(node, SwitchStatement(Visit(node.Begin.Expression)).WithSections(List(VisitCaseBlocks(node.CaseBlocks))))

            End Function

            Public Overrides Function VisitSelectClause(node As VB.SelectClauseSyntax) As CS.SyntaxNode
                Return MyBase.VisitSelectClause(node)
            End Function

            Public Overrides Function VisitSelectStatement(node As VB.SelectStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitSimpleArgument(node As VB.SimpleArgumentSyntax) As CS.SyntaxNode

                If TypeOf node.Parent.Parent Is VB.AttributeSyntax Then
                    Return AttributeArgument(Visit(node.Expression))
                Else
                    ' TODO: Bind to discover ByRef arguments.
                    Return CS.Syntax.Argument(Visit(node.Expression))
                End If

            End Function

            Public Overrides Function VisitSimpleAsClause(node As VB.SimpleAsClauseSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return Visit(node.Type)

            End Function

            Public Overrides Function VisitSingleLineElsePart(node As VB.SingleLineElsePartSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitSingleLineIfStatement(node As VB.SingleLineIfStatementSyntax) As CS.SyntaxNode

                Dim elseOpt As CS.ElseClauseSyntax

                If node.ElsePart IsNot Nothing Then
                    elseOpt = ElseClause(Block(List(Visit(node.ElsePart.Statements))))
                End If

                Return TransferTrivia(node, IfStatement(
                                                Visit(node.IfPart.Begin.Condition),
                                                Block(List(Visit(node.IfPart.Statements)))) _
                                              .WithElse(elseOpt)
                                        )


            End Function

            Public Overrides Function VisitSingleLineLambdaExpression(node As VB.SingleLineLambdaExpressionSyntax) As CS.SyntaxNode

                If node.Kind = VB.SyntaxKind.SingleLineFunctionLambdaExpression Then

                    Return ParenthesizedLambdaExpression(Visit(node.Body)).WithParameterList(VisitParameterList(node.Begin.ParameterList))

                Else

                    Return ParenthesizedLambdaExpression(Block(List(Visit(node.Body)))).WithParameterList(VisitParameterList(node.Begin.ParameterList))

                End If

            End Function

            Public Overrides Function VisitSkippedTokensTrivia(node As VB.SkippedTokensTriviaSyntax) As CS.SyntaxNode

                Return SkippedTokensTrivia(TokenList(MissingToken(CS.SyntaxKind.SemicolonToken).WithTrailingTrivia(TriviaList(Comment("/* " & node.ToString() & " */")))))

            End Function

            Public Overrides Function VisitSpecialConstraint(node As VB.SpecialConstraintSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.NewConstraint
                        Return ConstructorConstraint()
                    Case VB.SyntaxKind.ClassConstraint
                        Return ClassOrStructConstraint(CS.SyntaxKind.ClassConstraint)
                    Case VB.SyntaxKind.StructureConstraint
                        Return ClassOrStructConstraint(CS.SyntaxKind.StructConstraint)
                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitStopOrEndStatement(node As VB.StopOrEndStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)
                ' TODO: Rewrite Stop to System.Diagnostics.Debug.Break and End to System.Environment.Exit.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitSyncLockBlock(node As VB.SyncLockBlockSyntax) As CS.SyntaxNode

                Return VisitSyncLockStatement(node.Begin)

            End Function

            Public Overrides Function VisitSyncLockStatement(node As VB.SyncLockStatementSyntax) As CS.SyntaxNode

                Dim syncLockBlock As VB.SyncLockBlockSyntax = node.Parent

                Return LockStatement(Visit(node.Expression), Block(List(Visit(syncLockBlock.Statements))))

            End Function

            Public Overrides Function VisitTernaryConditionalExpression(node As VB.TernaryConditionalExpressionSyntax) As CS.SyntaxNode

                Return ConditionalExpression(Visit(node.Condition), Visit(node.WhenTrue), Visit(node.WhenFalse))

            End Function

            Public Overrides Function VisitThrowStatement(node As VB.ThrowStatementSyntax) As CS.SyntaxNode

                If node.Expression Is Nothing Then
                    Return ThrowStatement()
                Else
                    Return ThrowStatement(Visit(node.Expression))
                End If

            End Function

            Protected Function VisitTrivia(trivia As VB.SyntaxTrivia) As CS.SyntaxTrivia

                Dim text = trivia.ToFullString()

                Select Case trivia.Kind
                    Case VB.SyntaxKind.CommentTrivia

                        If text.StartsWith("'") AndAlso text.Length > 1 Then
                            Return Comment("//" & text.Substring(1))
                        ElseIf text.StartsWith("REM", StringComparison.OrdinalIgnoreCase) AndAlso text.Length > 3 Then
                            Return Comment("//" & text.Substring(3))
                        Else
                            Return Comment("//")
                        End If

                    Case VB.SyntaxKind.DisabledTextTrivia

                        Return Comment("/* Disabled: " & text & " */")

                    Case VB.SyntaxKind.EndOfLineTrivia, VB.SyntaxKind.ImplicitLineContinuationTrivia

                        Return EndOfLine(text)

                    Case VB.SyntaxKind.DocumentationCommentTrivia

                        Return CS.Syntax.Trivia(VisitDocumentationCommentTrivia(trivia.GetStructure()))

                    Case VB.SyntaxKind.WhitespaceTrivia

                        Return Whitespace(text)

                    Case Else

                        Return Comment("/* " & text & " */")

                End Select
            End Function

            Protected Function VisitTrivia(trivia As IEnumerable(Of VB.SyntaxTrivia)) As CS.SyntaxTriviaList

                Return TriviaList(From t In trivia Select VisitTrivia(t))

            End Function

            Public Overrides Function VisitTryBlock(node As VB.TryBlockSyntax) As CS.SyntaxNode

                Return TransferTrivia(node, TryStatement(List(VisitCatchParts(node.CatchParts))) _
                                                .WithBlock(VisitTryPart(node.TryPart)) _
                                                .WithFinally(VisitFinallyPart(node.FinallyPart))
                                            )

            End Function

            Public Overrides Function VisitTryPart(node As VB.TryPartSyntax) As CS.SyntaxNode

                Return Block(List(Visit(node.Statements)))

            End Function

            Public Overrides Function VisitTryStatement(node As VB.TryStatementSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Public Overrides Function VisitTypeArgumentList(node As VB.TypeArgumentListSyntax) As CS.SyntaxNode

                Return TypeArgumentList(Visit(node.Arguments).ToCommaSeparatedList(Of CS.TypeSyntax)())

            End Function

            Public Overrides Function VisitModuleBlock(ByVal node As VB.ModuleBlockSyntax) As CS.SyntaxNode

                Return VisitModuleStatement(node.Begin)

            End Function

            Public Overrides Function VisitClassBlock(ByVal node As VB.ClassBlockSyntax) As CS.SyntaxNode

                Return VisitClassStatement(node.Begin)

            End Function

            Public Overrides Function VisitStructureBlock(ByVal node As VB.StructureBlockSyntax) As CS.SyntaxNode

                Return VisitStructureStatement(node.Begin)

            End Function

            Public Overrides Function VisitInterfaceBlock(ByVal node As VB.InterfaceBlockSyntax) As CS.SyntaxNode

                Return VisitInterfaceStatement(node.Begin)

            End Function

            Public Overrides Function VisitTypeConstraint(ByVal node As VB.TypeConstraintSyntax) As CS.SyntaxNode

                Return TypeConstraint(Visit(node.Type))

            End Function

            Public Overrides Function VisitTypeOfExpression(node As VB.TypeOfExpressionSyntax) As CS.SyntaxNode

                Dim isExpression = BinaryExpression(CS.SyntaxKind.IsExpression, Visit(node.Expression), Visit(node.Type))

                If node.Kind = Roslyn.Compilers.VisualBasic.SyntaxKind.TypeOfIsNotExpression Then
                    Return PrefixUnaryExpression(Roslyn.Compilers.CSharp.SyntaxKind.NegateExpression,
                                                 ParenthesizedExpression(isExpression)
                           )
                Else
                    Return isExpression
                End If

            End Function

            Public Overrides Function VisitTypeParameter(node As VB.TypeParameterSyntax) As CS.SyntaxNode

                Dim varianceKeyword As CS.SyntaxToken
                Select Case node.VarianceKeyword.Kind
                    Case VB.SyntaxKind.InKeyword
                        varianceKeyword = Token(CS.SyntaxKind.InKeyword)

                    Case VB.SyntaxKind.OutKeyword
                        varianceKeyword = Token(CS.SyntaxKind.OutKeyword)

                    Case Else
                        varianceKeyword = Token(CS.SyntaxKind.None)
                End Select

                Return TypeParameter(VisitIdentifier(node.Identifier)).WithVarianceKeyword(varianceKeyword)

            End Function

            Public Overrides Function VisitTypeParameterList(node As VB.TypeParameterListSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return TypeParameterList(Visit(node.Parameters).ToCommaSeparatedList(Of CS.TypeParameterSyntax))

            End Function

            Protected Function VisitTypeParameterConstraintClauses(typeParameterListOpt As VB.TypeParameterListSyntax) As IEnumerable(Of CS.SyntaxNode)

                If typeParameterListOpt Is Nothing Then Return Nothing

                Return Visit((From parameter In typeParameterListOpt.Parameters Where parameter.TypeParameterConstraintClause IsNot Nothing Select parameter.TypeParameterConstraintClause))

            End Function

            Public Overrides Function VisitTypeParameterMultipleConstraintClause(node As VB.TypeParameterMultipleConstraintClauseSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                ' In C# the new() constraint must be specified last.
                Return TypeParameterConstraintClause(IdentifierName(VisitIdentifier(CType(node.Parent, VB.TypeParameterSyntax).Identifier))).WithConstraints(Visit((From c In node.Constraints Order By c.Kind = VB.SyntaxKind.NewConstraint)).ToCommaSeparatedList(Of CS.TypeParameterConstraintSyntax)())

            End Function

            Public Overrides Function VisitTypeParameterSingleConstraintClause(node As VB.TypeParameterSingleConstraintClauseSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                Return TypeParameterConstraintClause(IdentifierName(VisitIdentifier(CType(node.Parent, VB.TypeParameterSyntax).Identifier))).WithConstraints({Visit(node.Constraint)}.ToCommaSeparatedList(Of CS.TypeParameterConstraintSyntax)())

            End Function

            Public Overrides Function VisitModuleStatement(ByVal node As VB.ModuleStatementSyntax) As CS.SyntaxNode
                Dim block As VB.ModuleBlockSyntax = node.Parent

                ' TODO: Rewrite all members in a module to be static.
                Return TransferTrivia(block, ClassDeclaration(VisitIdentifier(node.Identifier)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                                                .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList))) _
                                                .WithMembers(List(Visit(block.Members)))
                                             )

            End Function

            Public Overrides Function VisitClassStatement(ByVal node As VB.ClassStatementSyntax) As CS.SyntaxNode

                Dim block As VB.ClassBlockSyntax = node.Parent

                Dim bases As CS.BaseListSyntax
                If block.Inherits.Count > 0 OrElse block.Implements.Count > 0 Then
                    bases = BaseList(VisitInheritsStatements(block.Inherits).Union(VisitImplementsStatements(block.Implements)).ToCommaSeparatedList(Of CS.TypeSyntax))
                End If

                Return TransferTrivia(block, ClassDeclaration(VisitIdentifier(node.Identifier)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                                                .WithBaseList(bases) _
                                                .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList))) _
                                                .WithMembers(List(Visit(block.Members)))
                                             )

            End Function

            Public Overrides Function VisitStructureStatement(ByVal node As VB.StructureStatementSyntax) As CS.SyntaxNode

                Dim block As VB.StructureBlockSyntax = node.Parent

                Dim bases As CS.BaseListSyntax
                If block.Inherits.Count > 0 OrElse block.Implements.Count > 0 Then
                    bases = BaseList(VisitInheritsStatements(block.Inherits).Union(VisitImplementsStatements(block.Implements)).ToCommaSeparatedList(Of CS.TypeSyntax))
                End If

                Return TransferTrivia(block, StructDeclaration(VisitIdentifier(node.Identifier)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                                                .WithBaseList(bases) _
                                                .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList))) _
                                                .WithMembers(List(Visit(block.Members)))
                                        )

            End Function

            Public Overrides Function VisitInterfaceStatement(ByVal node As VB.InterfaceStatementSyntax) As CS.SyntaxNode

                Dim block As VB.InterfaceBlockSyntax = node.Parent

                Dim bases As CS.BaseListSyntax
                If block.Inherits.Count > 0 Then
                    bases = BaseList(VisitInheritsStatements(block.Inherits).ToCommaSeparatedList(Of CS.TypeSyntax))
                End If

                ' VB allows Interfaces to have nested types, C# does not. 
                ' But this is rare enough that we'll assume the members are non-types for now.
                Return TransferTrivia(block, InterfaceDeclaration(VisitIdentifier(node.Identifier)) _
                                                .WithAttributeLists(List(VisitAttributeLists(node.AttributeLists))) _
                                                .WithModifiers(TokenList(VisitModifiers(node.Modifiers))) _
                                                .WithTypeParameterList(VisitTypeParameterList(node.TypeParameterList)) _
                                                .WithBaseList(bases) _
                                                .WithConstraintClauses(List(VisitTypeParameterConstraintClauses(node.TypeParameterList))) _
                                                .WithMembers(List(Visit(block.Members)))
                                             )
            End Function

            Public Overrides Function VisitUnaryExpression(ByVal node As VB.UnaryExpressionSyntax) As CS.SyntaxNode

                Select Case node.Kind
                    Case VB.SyntaxKind.NegateExpression

                        Return PrefixUnaryExpression(CS.SyntaxKind.NegateExpression, Visit(node.Operand))

                    Case VB.SyntaxKind.PlusExpression

                        Return PrefixUnaryExpression(CS.SyntaxKind.PlusExpression, Visit(node.Operand))

                    Case VB.SyntaxKind.NotExpression

                        ' TODO: Bind expression to determine whether this is a logical or bitwise not expression.
                        Return PrefixUnaryExpression(CS.SyntaxKind.LogicalNotExpression, Visit(node.Operand))

                    Case VB.SyntaxKind.AddressOfExpression

                        Return Visit(node.Operand)

                    Case Else
                        Throw New NotSupportedException(node.Kind.ToString())
                End Select

            End Function

            Public Overrides Function VisitUsingBlock(node As VB.UsingBlockSyntax) As CS.SyntaxNode

                Return VisitUsingStatement(node.Begin)

            End Function

            Public Overrides Function VisitUsingStatement(node As VB.UsingStatementSyntax) As CS.SyntaxNode

                Dim usingBlock As VB.UsingBlockSyntax = node.Parent

                Dim body As CS.StatementSyntax = Block(List(Visit(usingBlock.Statements)))

                If node.Expression IsNot Nothing Then

                    Return TransferTrivia(usingBlock, UsingStatement(body).WithExpression(Visit(node.Expression)))

                Else

                    For i = node.Variables.Count - 1 To 0 Step -1

                        Dim declarator = node.Variables(i)

                        ' TODO: Refactor so that visiting a VB declarator returns a C# declarator.
                        body = UsingStatement(body).WithDeclaration(
                                   VariableDeclaration(
                                       DeriveType(declarator.Names(0), declarator.AsClause, declarator.Initializer),
                                       {VariableDeclarator(
                                            VisitIdentifier(declarator.Names(0).Identifier)) _
                                          .WithInitializer(DeriveInitializer(declarator.Names(0), declarator.AsClause, declarator.Initializer))
                                       }.ToCommaSeparatedList())
                               )

                    Next

                    Return TransferTrivia(node, body)
                End If

            End Function

            Public Overrides Function VisitVariableDeclarator(node As VB.VariableDeclaratorSyntax) As CS.SyntaxNode

                Throw New InvalidOperationException()

            End Function

            Protected Function VisitVariableDeclaratorVariables(declarator As VB.VariableDeclaratorSyntax) As IEnumerable(Of CS.SyntaxNode)

                ' TODO: Derive an initializer based on VB's As New syntax or default variable
                ' initialization.
                Select Case declarator.Parent.Kind
                    Case VB.SyntaxKind.FieldDeclaration
                        Dim field As VB.FieldDeclarationSyntax = declarator.Parent

                        Return From v In declarator.Names Select FieldDeclaration(
                                                                     VariableDeclaration(
                                                                         DeriveType(v, declarator.AsClause, declarator.Initializer),
                                                                         {VariableDeclarator(
                                                                              VisitIdentifier(v.Identifier)).WithInitializer(
                                                                              DeriveInitializer(v, declarator.AsClause, declarator.Initializer)
                                                                          )
                                                                         }.ToCommaSeparatedList())
                                                                     ).WithAttributeLists(List(VisitAttributeLists(field.AttributeLists))) _
                                                                      .WithModifiers(TokenList(VisitModifiers(field.Modifiers)))
                    Case VB.SyntaxKind.LocalDeclarationStatement
                        Dim local As VB.LocalDeclarationStatementSyntax = declarator.Parent

                        Return From v In declarator.Names Select LocalDeclarationStatement(
                                                                     VariableDeclaration(
                                                                         DeriveType(v, declarator.AsClause, declarator.Initializer),
                                                                         {VariableDeclarator(
                                                                              VisitIdentifier(v.Identifier)).WithInitializer(
                                                                                 DeriveInitializer(v, declarator.AsClause, declarator.Initializer)
                                                                                )
                                                                         }.ToCommaSeparatedList())).WithModifiers(TokenList(VisitModifiers(local.Modifiers)))

                    Case Else
                        Throw New NotSupportedException(declarator.Parent.Kind.ToString())
                End Select
            End Function

            Public Overrides Function VisitVariableNameEquals(node As VB.VariableNameEqualsSyntax) As CS.SyntaxNode
                Return MyBase.VisitVariableNameEquals(node)
            End Function

            Public Overrides Function VisitWhereClause(node As VB.WhereClauseSyntax) As CS.SyntaxNode

                Return WhereClause(Visit(node.Condition))

            End Function

            Public Overrides Function VisitWhileBlock(node As VB.WhileBlockSyntax) As CS.SyntaxNode

                Return VisitWhileStatement(node.Begin)

            End Function

            Public Overrides Function VisitWhileStatement(node As VB.WhileStatementSyntax) As CS.SyntaxNode

                Dim whileBlock As VB.WhileBlockSyntax = node.Parent

                Return TransferTrivia(node, WhileStatement(Visit(node.Condition), Block(List(Visit(whileBlock.Statements)))))

            End Function

            Public Overrides Function VisitWhileUntilClause(node As VB.WhileUntilClauseSyntax) As CS.SyntaxNode

                If node Is Nothing Then Return Nothing

                If node.Kind = VB.SyntaxKind.WhileClause Then
                    Return Visit(node.Condition)
                Else
                    ' TODO: Invert conditionals if possible on comparison expressions to avoid wrapping this in a !expression.
                    Return PrefixUnaryExpression(CS.SyntaxKind.LogicalNotExpression, ParenthesizedExpression(Visit(node.Condition)))
                End If

            End Function

            Public Overrides Function VisitWithBlock(node As VB.WithBlockSyntax) As CS.SyntaxNode

                Return VisitWithStatement(node.Begin)

            End Function

            Public Overrides Function VisitWithStatement(node As VB.WithStatementSyntax) As CS.SyntaxNode

                Return NotImplementedStatement(node)
                ' TODO: Rewrite to block with temp variable name instead of omitted LeftOpt member access expressions.
                Throw New NotImplementedException(node.ToString())

            End Function

            Public Overrides Function VisitXmlAttribute(node As VB.XmlAttributeSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlAttribute(node)
            End Function

            Public Overrides Function VisitXmlBracketedName(node As VB.XmlBracketedNameSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlBracketedName(node)
            End Function

            Public Overrides Function VisitXmlCDataSection(node As VB.XmlCDataSectionSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlComment(node As VB.XmlCommentSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlDeclaration(node As VB.XmlDeclarationSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlDeclaration(node)
            End Function

            Public Overrides Function VisitXmlDeclarationOption(node As VB.XmlDeclarationOptionSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlDeclarationOption(node)
            End Function

            Public Overrides Function VisitXmlDocument(node As VB.XmlDocumentSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlElement(node As VB.XmlElementSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlElementEndTag(node As VB.XmlElementEndTagSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlElementEndTag(node)
            End Function

            Public Overrides Function VisitXmlElementStartTag(node As VB.XmlElementStartTagSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlElementStartTag(node)
            End Function

            Public Overrides Function VisitXmlEmbeddedExpression(node As VB.XmlEmbeddedExpressionSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlEmbeddedExpression(node)
            End Function

            Public Overrides Function VisitXmlEmptyElement(node As VB.XmlEmptyElementSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlMemberAccessExpression(node As VB.XmlMemberAccessExpressionSyntax) As CS.SyntaxNode
                Return NotImplementedExpression(node)
            End Function

            Public Overrides Function VisitXmlName(node As VB.XmlNameSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlName(node)
            End Function

            Public Overrides Function VisitXmlNamespaceImportsClause(node As VB.XmlNamespaceImportsClauseSyntax) As CS.SyntaxNode

                Return UsingDirective(IdentifierName(MissingToken(CS.SyntaxKind.IdentifierToken))) _
                    .WithUsingKeyword(MissingToken(CS.SyntaxKind.UsingKeyword)) _
                    .WithSemicolonToken(MissingSemicolonToken.WithTrailingTrivia(TriviaList(Comment("/* " & node.ToString() & " */"))))

            End Function

            Protected Overridable Function VisitXmlNode(node As VB.XmlNodeSyntax) As CS.SyntaxNode
                ' Just spit this out as a string literal for now.
                Dim text = node.ToString().Replace("""", """""")

                Return LiteralExpression(CS.SyntaxKind.StringLiteralExpression, Literal("@""" & text & """", text))
            End Function

            Public Overrides Function VisitXmlPrefix(node As VB.XmlPrefixSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlPrefix(node)
            End Function

            Public Overrides Function VisitXmlProcessingInstruction(node As VB.XmlProcessingInstructionSyntax) As CS.SyntaxNode
                Return VisitXmlNode(node)
            End Function

            Public Overrides Function VisitXmlString(node As VB.XmlStringSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlString(node)
            End Function

            Public Overrides Function VisitXmlText(node As VB.XmlTextSyntax) As CS.SyntaxNode
                Return MyBase.VisitXmlText(node)
            End Function

            Protected Function NotImplementedStatement(node As VB.SyntaxNode) As CS.StatementSyntax
                Return EmptyStatement(MissingSemicolonToken.WithTrailingTrivia(TriviaList(Comment("/* Not Implemented: " & node.ToString() & " */"))))
            End Function

            Protected Function NotImplementedMember(node As VB.SyntaxNode) As CS.MemberDeclarationSyntax
                Return IncompleteMember().WithModifiers(TokenList(MissingToken(CS.SyntaxKind.PublicKeyword).WithTrailingTrivia(TriviaList(Comment("/* Not Implemented: " & node.ToString() & " */")))))
            End Function

            Protected Function NotImplementedExpression(node As VB.SyntaxNode) As CS.ExpressionSyntax
                Return IdentifierName(MissingToken(CS.SyntaxKind.IdentifierToken).WithTrailingTrivia(TriviaList(Comment("/* Not Implemented: " & node.ToString() & " */"))))
            End Function

            Protected Function NotImplementedModifier(token As VB.SyntaxToken) As CS.SyntaxToken
                Return MissingToken(CS.SyntaxKind.PublicKeyword).WithTrailingTrivia(TriviaList(Comment("/* Not Implemented: " & token.ToString() & " */")))
            End Function

        End Class

    End Class

    Friend Module SyntaxUtils

        ReadOnly CommaToken As CS.SyntaxToken = Token(CS.SyntaxKind.CommaToken)
        ReadOnly OmittedArraySizeExpression As CS.SyntaxNode = CS.Syntax.OmittedArraySizeExpression(Token(CS.SyntaxKind.OmittedArraySizeExpressionToken))

        Public Function OmittedArraySizeExpressionList(Of TNode As CS.SyntaxNode)(rank As Integer) As CS.SeparatedSyntaxList(Of TNode)

            Dim tokens = New CS.SyntaxNodeOrToken(0 To 2 * rank - 2) {}
            For i = 0 To rank - 2
                tokens(2 * i) = OmittedArraySizeExpression
                tokens(2 * i + 1) = CommaToken
            Next

            tokens(2 * rank - 2) = OmittedArraySizeExpression

            Return SeparatedList(Of TNode)(tokens)

        End Function

        <Extension()>
        Public Function ToCommaSeparatedList(Of TNode As CS.SyntaxNode)(nodes As IEnumerable(Of TNode)) As CS.SeparatedSyntaxList(Of TNode)

            Return SeparatedList(nodes, From node In nodes Skip 1 Select CommaToken)

        End Function

        <Extension()>
        Public Function ToCommaSeparatedList(Of TNode As CS.SyntaxNode, TResult As TNode)(nodes As IEnumerable(Of TNode)) As CS.SeparatedSyntaxList(Of TResult)
            Return nodes.Cast(Of TResult).ToCommaSeparatedList()
        End Function

        <Extension()>
        Public Function WithLeadingTrivia(Of TNode As CS.SyntaxNode)(node As TNode, trivia As IEnumerable(Of CS.SyntaxTrivia)) As TNode
            Dim firstToken = node.GetFirstToken()

            Return node.ReplaceToken(firstToken, firstToken.WithLeadingTrivia(TriviaList(trivia)))
        End Function

        <Extension()>
        Public Function WithTrailingTrivia(Of TNode As CS.SyntaxNode)(node As TNode, trivia As IEnumerable(Of CS.SyntaxTrivia)) As TNode
            Dim lastToken = node.GetLastToken()

            Return node.ReplaceToken(lastToken, lastToken.WithTrailingTrivia(TriviaList(trivia)))
        End Function

        <Extension()>
        Public Function WithTrivia(Of TNode As CS.SyntaxNode)(node As TNode, leadingTrivia As IEnumerable(Of CS.SyntaxTrivia), trailingTrivia As IEnumerable(Of CS.SyntaxTrivia)) As TNode
            Return node.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia)
        End Function

    End Module

End Namespace
