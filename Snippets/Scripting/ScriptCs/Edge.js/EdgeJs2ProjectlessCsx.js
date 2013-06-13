var edge = require('edge');

var csx = edge.func("ProjectlessCsx.csx");

var payload = {
	platform: 'Node.js',
	something: function(input, callback) {
		callback(null, "callback to Node.js");
	}
};

// Make asynchronous call to projectless csx
csx(payload, function(error, result) {
	if(error) throw error;
	console.log(result);
});

console.log("Node.js continues!");