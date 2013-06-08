MetaProgramming .Net samples
==================================

#Metaprogramming via scripting
* [Brilliant sample from 'Metaprogramming in .Net' book](Snippets/Scripting/JsEvalSample.html)
* Scripting: Roslyn CTP vs. IronPython
* [ScriptCS `eval()`-like](Snippets/Scripting/Roslyn-EvalSample.csx)
* [Roslyn CTP: scripting and results as `dynamic`](Snippets/Scripting/Roslyn-ScriptingWithDynamiclyTypeResult.linq)
* IronPython: scripting, DLR and results as `dynamic`
  * [C# code](Snippets/Scripting/IronPython-Scripting.linq)
  * [.py code](Snippets/Scripting/sample.py)

#Dynamicly generating .Net assembly at runtime
* [Roslyn CTP: build and load assembly in memory](Snippets/Caas/Roslyn-BuildAtRuntime.linq)
* CodeDOM
  * [build and load assembly in memory](Snippets/Caas/CodeDOM-BuildAtRuntime.linq)
  * [C# 4.0 and 5.0 features](Snippets/Caas/CodeDOM-BuildAtRuntimeCSharp45.linq), not supported by Roslyn CTP yet

#Metaprogramming via runtime code generation
* [Hardcoded C# rule: ~900ms to run 1000 iterations](Snippets/Performance/C#-HardCodedRule.linq)
* [Roslyn CTP: scripting and staticly typed results: ~950ms to run 1000 iterations](Snippets/Performance/Roslyn-ScriptingWithStaticlyTypedResult.linq)
* [DLR i.e. Dynamic Language Runtime with ExpressionTrees: ~1200ms to run 1000 iterations](Snippets/Performance/DLR-ExpressionTrees.linq)

#Code-as-Data approach
* [Roslyn CTP convert VB → C#](Snippets/CodeAsData/Roslyn-ConvertVB2C#.linq), and [back](Snippets/CodeAsData/Roslyn-ConvertC#2VB.linq)
* [Roslyn CTP and T4](https://github.com/akimboyko/MetaProgramming/tree/master/MetaProgramming)
  * Runtime template transformation using T4
  * Generate types, models and algorithms from JSON input using Roslyn CTP

#Introspection
* Roslyn CTP: Calculate code complexity
  * [asynchronously](Snippets/Introspection/Roslyn-CyclomaticComplexity.linq)
  * [using Rx](Snippets/Introspection/Roslyn-CyclomaticComplexityRx.linq)
* Roslyn CTP: search for `return null;`. 
  * [asynchronously](Snippets/Introspection/Roslyn-ReturnNull.linq)
  * [using Rx](Snippets/Introspection/Roslyn-ReturnNullRx.linq)
  * [using Syntax Walker](Snippets/Introspection/Roslyn-ReturnNullSyntaxWalker.linq)
  * [Full sample with `yeild return null;`, `default(T)` and value/reference types check is here](https://github.com/akimboyko/MetaProgramming/blob/7d4d8533d2be673fad2fbad37bb4d7a75399519a/MetaProgramming/MetaProgramming.RoslynCTP/Introspection.cs)
* Using `System.Reflection` to limit dependencies of assemblies
  * [Test dependencies of assemblies intersection](/Snippets/Introspection/Reflection-TestReferencesIntersection.linq)

#Nemerle: compile-time macro
* Nemerle macro: compile- with run- time execution
  * [compile-time macro](MetaProgramming/MetaProgramming.Nemerle.Macro/TestMacro.n)
  * [compile-time macro usage](MetaProgramming/MetaProgramming.Nemerle/CompileTimeVsRunTimeExecutionSample.n)
  * [introduce new syntax keyword `fault`](MetaProgramming/MetaProgramming.Nemerle.Macro/Fault.n)
  * [new syntax keyword `fault` usage](MetaProgramming/MetaProgramming.Nemerle/FaultKeywordSample.n)
