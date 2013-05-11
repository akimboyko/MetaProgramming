using System;

namespace CodeSmells.Samples
{
    public class ClassWithExtraComplexity
    {
        public string SimpleMethod()
        {
            return @"Simple Method with Cyclomatic complexity == 1";
        }

        public string ComplexMethod()
        {
            int seed = new Random(DateTime.Now.Millisecond).Next() % 10;

            if (seed == 0)
            {
                return @"Case 0";
            }
            
            if (seed == 1)
            {
                return @"Case 1";
            }
            
            if (seed == 2)
            {
                return @"Case 2";
            }
            
            if (seed == 3)
            {
                return @"Case 3";
            }
            
            if (seed == 4)
            {
                return @"Case 4";
            }
            
            if (seed == 5)
            {
                return @"Case 5";
            }
            
            if (seed == 6)
            {
                return @"Case 6";
            }
            
            if (seed == 7)
            {
                return @"Case 7";
            }
            
            if (seed == 8)
            {
                return @"Case 8";
            }
            
            if (seed == 9)
            {
                return @"Case 9";
            }

            return @"Default case";
        }
    }
}