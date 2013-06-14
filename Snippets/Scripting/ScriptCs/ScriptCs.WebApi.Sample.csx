// 1. run shell with administrative rights, to be able to open port for listening
// 2. install WebApi script pack: `scriptcs -install ScriptCs.WebApi`
// 3. run script: `scriptcs.exe .\ScriptCs.WebApi.Sample.csx`

using System;
using System.Web.Http;
using System.Web.Http.SelfHost;

// will listen at http://localhost:8888/test/
public class TestController : ApiController
{
    public string Get()
    {
        return "Hello world from ScriptCs.WebApi!";
    }
}

// require/load script pack for WebAPI
var webApi = Require<WebApi>();
var server = webApi.CreateServer("http://localhost:8888");
server.OpenAsync().Wait();

Console.WriteLine("Listening...");
Console.ReadKey();
server.CloseAsync().Wait();