// ScriptCs sample with Newtonsoft.Json — just for test
using Newtonsoft.Json;

var message = new { messaeg = "Hello, new JSON world!", timestamp = DateTime.Now };

Console.WriteLine(
			JsonConvert.SerializeObject(
				message, 
				Newtonsoft.Json.Formatting.Indented));