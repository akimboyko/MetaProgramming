MetaProgramming .Net samples
==================================

#Metaprogramming via scripting
* [Brilliant sample from 'Metaprogramming in .Net' book](Snippets/JsEvalSample.html)
* Scripting: Roslyn CTP vs. IronPython
* [ScriptCS `eval()`-like](Snippets/Roslyn-EvalSample.csx)
* [Roslyn CTP: scripting and results as `dynamic`](Snippets/Roslyn-ScriptingWithDynamiclyTypeResult.linq)
* IronPython: scripting, DLR and results as `dynamic`
  * [C# code](IronPython-Scripting.linq)
  * [.py code](sample.py)

#Dynamicly generating .Net assembly at runtime
* [Roslyn CTP: build and load assembly in memory](Snippets/Roslyn-BuildAtRuntime.linq)
* CodeDOM
  * [build and load assembly in memory](Snippets/CodeDOM-BuildAtRuntime.linq)
  * [C# 4.0 and 5.0 features](Snippets/CodeDOM-BuildAtRuntimeCSharp45.linq), not supported by Roslyn CTP yet

#Metaprogramming via runtime code generation
* [Hardcoded C# rule: ~900ms to run 1000 iterations](Snippets/C#-HardCodedRule.linq)
* [Roslyn CTP: scripting and staticly typed results: ~950ms to run 1000 iterations](Snippets/Roslyn-ScriptingWithStaticlyTypedResult.linq)
* [DLR i.e. Dynamic Language Runtime with ExpressionTrees: ~1200ms to run 1000 iterations](Snippets/DLR-ExpressionTrees.linq)

#Code-as-Data approach
* [Roslyn CTP convert VB → C#](Snippets/Roslyn-ConvertVB2C#.linq), and [back](Snippets/Roslyn-ConvertC#2VB.linq)
* [Roslyn CTP and T4](https://github.com/akimboyko/MetaProgramming/tree/master/MetaProgramming)
  * Runtime template transformation using T4
  * Generate types, models and algorithms from JSON input using Roslyn CTP

#Introspection
* Roslyn CTP: Calculate code complexity
  * [asynchronously](Snippets/Roslyn-CyclomaticComplexity.linq)
  * [using Rx](Snippets/Roslyn-CyclomaticComplexityRx.linq)
