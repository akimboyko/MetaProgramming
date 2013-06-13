var edge = require('edge');

// Call C# async lambda from node.js
// Compile source code on the fly
// Use C# dynamics to access objects passed from JavaScript
var funcCs = edge.func('cs', function() { /*
	using System.Threading.Tasks;

	async (dynamic input) => 
	{
		// with dynamics and callbacks
		return new 
		{
			message = string.Format(@"C# welcomes {0}, {1} happens at {2}",
								input.platform.ToString(), 
								await input.something(null),
								DateTime.Now),
			origin = "from C#"
		};
	}
*/});

// Use C# function at Node.js
var callbackFromCs = edge.func(function () {/*
    async (input) => {
    	// C# function is also data
        return (Func<object,Task<object>>)(async (i) => {
        	return "Callback from .NET returned to Node.Js";
        });
    }
*/});

// Pass data from node.js to C#
var payload = {
	platform: 'Node.js',

	// Node.js function is also data
	something: function(input, callback) { 
		callback(null, "callback to Node.js");
	}
};

// Make asynchronous call to C# function
funcCs(payload, function(error, result) {
	if(error) throw error;
	console.log(result);
});

// C# function is also data, call it from Node.js
var callbackCs = callbackFromCs(null, true); 
console.log(callbackCs(null, true));