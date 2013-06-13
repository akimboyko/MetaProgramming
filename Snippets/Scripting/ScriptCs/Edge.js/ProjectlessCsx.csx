// use #r "SomeDotNetLibrary.dll" to load assembly

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Edge.js has floowing convention:
// start `class Startup::public async Task<object> Invoke(object input)`
public class Startup
{
	public async Task<object> Invoke(object input)
	{
		// we are on V8 thread here
		var dictionary = (IDictionary<string, object>)input;

		return await Task.Run<object>(async() => {

			Console.WriteLine("CLR simulates CPU bound operation");
			Console.Out.FlushAsync();

			// we are on CLR thread pool thread here
			await Task.Delay(2000); // simulate CPU bound  

			Console.WriteLine("CLR continues...");
			Console.Out.FlushAsync();

			return new
			{
				message =
					string.Format(@"CSX welcomes {0}, {1} happens at {2}",
								dictionary["platform"].ToString(),
								await(dictionary["something"] as Func<object, Task<object>>)(null),
								DateTime.Now),
				origin = "from Projectless CSX"
			};
		});
    }
}