#JavaScript metaprogramming via scripting
From [book 'Metaprogramming in .NET' by Kevin Hazzard and Jason Bock](http://www.manning.com/hazzard/)

```javascript
function convert() {
	// get _string_ value from `fromVal` textbox and _evaluate_ it
	var fromValue = eval(fromVal.value);

	// get _string_ value from `formula` textbox, 
	// and _evaluate_ using _local executing scope_
    toVal.innerHTML = eval(formula.value).toString();
}
```

#Simple multiplication
* fromValue: `6`  
* formula: `7 * fromValue`

#Using function `Math.sqrt` dynamically
* fromValue: `6`  
* formula: `Math.sqrt(7 * fromValue)`

#Injection values into local execution scope
* fromValue: `var someValue = 3`  
* formula: `Math.sqrt(7 * someValue)` 