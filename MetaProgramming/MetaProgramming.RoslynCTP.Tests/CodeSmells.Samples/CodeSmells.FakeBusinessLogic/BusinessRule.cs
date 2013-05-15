using System;
using NHibernate;

namespace CodeSmells.FakeBusinessLogic
{
    public class BusinessRule
    {
        public static readonly Type NHibernateSessionType = typeof(ISession);
    }
}
