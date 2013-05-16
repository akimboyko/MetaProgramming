namespace CodeSmells.FakeDataAccessLibrary.Entity
{
    public class Customer
    {
        // This property is defined as expected
        public virtual int Id { get; set; }

        // without `virtual` keyword PostSharp will generate compile-time error message
        // otherwise NHiberante will generate exception during run-time
        public /*virtual*/ string Name { get; set; } 

        // Both getter/setter are required to be public
        internal virtual string Description { get; private set; }
    }
}