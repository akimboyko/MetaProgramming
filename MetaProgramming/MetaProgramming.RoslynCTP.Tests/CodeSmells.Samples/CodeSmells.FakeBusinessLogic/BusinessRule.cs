using System;

namespace CodeSmells.FakeBusinessLogic
{
    public class BusinessRule
    {
        public static readonly Type NHibernateSessionType = typeof(NHibernate.ISession);
        public static readonly Type JsonSerializerType = typeof(Newtonsoft.Json.JsonSerializer);
    }
}
