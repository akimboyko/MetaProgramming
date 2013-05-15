using CodeSmells.FakeDataAccessLibrary.Aspect;

namespace CodeSmells.FakeDataAccessLibrary.Entity
{
    [VirtualKeywordRequiredForEntities]
    public class Customer : IEntity
    {
        public virtual int Id { get; set; }
        // without `virtual` keyword PostSharp will generate compile-time error message
        // otherwise NHiberante will generate exception during run-time
        public virtual string Name { get; set; } 
        public virtual string Description { get; set; }
    }
}