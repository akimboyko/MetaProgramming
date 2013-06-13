var edge = require('edge');

var helloCs = edge.func('cs', function() { /*
	using System.Collections.Generic;
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
			originl = "from C#"
		};
	}
*/});

var createHello = edge.func(function () {/*
    async (input) => {
        return (Func<object,Task<object>>)(async (i) => {
        	return "Callback from .NET returned to Node.Js";
        });
    }
*/});

var payload = {
	platform: 'Node.js',
	something: function(input, callback) {
		callback(null, "callback to Node.js");
	}
};

// Make asynchronous call
helloCs(payload, function(error, result) {
	if(error) throw error;
	console.log(result);
});

// C# function is also data
var hello = createHello(null, true); 
console.log(hello(null, true));