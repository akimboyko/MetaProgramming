using System;

namespace CodeSmells.FakeDataAccessLibrary
{
    public class Repository
    {
        public static readonly Type NHibernateSessionType = typeof(NHibernate.ISession);
    }
}
