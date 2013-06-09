using Newtonsoft.Json;

var message = new { messaeg = "Hello, world!", timestamp = DateTime.Now };

Console.WriteLine(
			JsonConvert.SerializeObject(
				message, 
				Newtonsoft.Json.Formatting.Indented));