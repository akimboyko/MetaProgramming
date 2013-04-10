MetaProgramming .Net samples
==================================

#Metaprogramming via scripting
* [Brilliant sample from 'Metaprogramming in .Net' book](Snippets/JsEvalSample.html)
* Scripting: RoslynCTP vs. IronPython
 * [RoslynCTP: scripting and results as `dynamic`](Snippets/Roslyn-ScriptingWithDynamiclyTypeResult.linq)
 * [IronPython: scripting, DLR and results as `dynamic`](IronPython-Scripting.linq)
 * [.py code](sample.py)

#Dynamicly generating .Net assembly at runtime
* [Build and load assembly in memory](Snippets/Roslyn-BuildAtRuntime.linq)

#Metaprogramming via runtime code generation
* [Hardcoded C# rule: ~900ms to run 1000 iterations](Snippets/C#-HardCodedRule.linq)
* [RoslynCTP: scripting and staticly typed results: ~950ms to run 1000 iterations](Snippets/Roslyn-ScriptingWithStaticlyTypedResult.linq)
* [DLR i.e. Dynamic Language Runtime with ExpressionTrees: ~1200ms to run 1000 iterations](Snippets/DLR-ExpressionTrees.linq)

#Introspection
* [Calculate code complexity asynchronously](Snippets/Roslyn-CyclomaticComplexity.linq)
* [Calculate code complexity using Rx](Snippets/Roslyn-CyclomaticComplexityRx.linq)
