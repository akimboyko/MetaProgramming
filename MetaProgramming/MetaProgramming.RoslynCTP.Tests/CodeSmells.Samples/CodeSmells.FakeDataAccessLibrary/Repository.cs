using System;
using NHibernate;

namespace CodeSmells.FakeDataAccessLibrary
{
    public class Repository
    {
        public static readonly Type NHibernateSessionType = typeof(ISession);
    }
}
