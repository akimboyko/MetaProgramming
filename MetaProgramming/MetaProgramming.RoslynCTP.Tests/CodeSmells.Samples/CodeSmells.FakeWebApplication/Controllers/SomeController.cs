using System.Collections.Generic;
using System.Web.Http;
using CodeSmells.FakeWebApplication.Aspect;

namespace CodeSmells.FakeWebApplication.Controllers
{
    // Require following HTTP verbs to have identity and signature
    [HmacSignatureRequired(new[] { "Post", "Put", "Delete" })]
    public class SomeController : ApiController
    {
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        public string Get(int id)
        {
            return "value";
        }

        public void Post([FromBody]int userId, [FromBody]string value, [FromBody]string hmacSignature)
        {
        }

        public void Put([FromBody]int userId, int id, [FromBody]string value, [FromBody]string hmacSignature)
        {
        }

        // without both `int userId` and `string hmacSignature` parameters 
        // PostSharp will generate compile-time error message
        public void Delete(int userId, int id, decimal hmacSignature)
        {
        }
    }
}