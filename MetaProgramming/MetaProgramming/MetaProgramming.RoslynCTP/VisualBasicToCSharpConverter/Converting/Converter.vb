' *********************************************************
'
' Copyright © Microsoft Corporation
'
' Licensed under the Apache License, Version 2.0 (the
' "License"); you may not use this file except in
' compliance with the License. You may obtain a copy of
' the License at
'
' http://www.apache.org/licenses/LICENSE-2.0 
'
' THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES
' OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
' INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES
' OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR
' PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
'
' See the Apache 2 License for the specific language
' governing permissions and limitations under the License.
'
' *********************************************************

Imports System.Collections.Generic
Imports CS = Roslyn.Compilers.CSharp
Imports VB = Roslyn.Compilers.VisualBasic

Namespace VisualBasicToCSharpConverter.Converting
    Public Class Converter
        Public Function Convert(
                            tree As VB.SyntaxTree,
                            Optional identifierMap As IDictionary(Of String, String) = Nothing,
                            Optional convertStrings As Boolean = False
                        ) As CS.SyntaxNode

            Return ConvertTree(tree)
        End Function

        Public Shared Function ConvertTree(tree As VB.SyntaxTree) As CS.SyntaxNode
            Return New NodeConvertingVisitor().Visit(tree.GetRoot())
        End Function
    End Class
End Namespace