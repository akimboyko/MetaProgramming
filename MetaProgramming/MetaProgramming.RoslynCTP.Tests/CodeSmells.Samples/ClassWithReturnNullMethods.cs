using System.Collections.Generic;

namespace CodeSmells.Samples
{
    public class ClassWithReturnNullMethods
    {
        public object ReturnSomeReferenceType()
        {
            return new object();
        }

        public object ReturnNullReferenceType()
        {
            return null;
        }

        public object ReturnDefaultReferenceType()
        {
            return default(object);
        }

        public IEnumerable<object> ReturnEnumerableOfDefaultReferenceType()
        {
            yield return default(object);
        }

        public int ReturnSomeValueType()
        {
            return new int();
        }

        public int ReturnDefaultValueType()
        {
            return default(int);
        }

        public IEnumerable<int> ReturnEnumerableOfDefaultValueType()
        {
            yield return default(int);
        }
    }
}
