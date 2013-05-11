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

        public int ReturnSomeValueType()
        {
            return new int();
        }

        public int ReturnDefaultValueType()
        {
            return default(int);
        }
    }
}
