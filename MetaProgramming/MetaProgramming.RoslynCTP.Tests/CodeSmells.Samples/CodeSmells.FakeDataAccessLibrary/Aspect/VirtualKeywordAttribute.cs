using System;
using System.Linq;
using System.Reflection;
using PostSharp;
using PostSharp.Constraints;
using PostSharp.Extensibility;

namespace CodeSmells.FakeDataAccessLibrary.Aspect
{
    [Serializable]
    [MulticastAttributeUsage(MulticastTargets.Class)]
    public class VirtualKeywordRequiredForEntitiesAttribute : ScalarConstraint
    {
        public override void ValidateCode(object target)
        {
            var targetType = (Type)target;

            var virtualProperties = targetType
                                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(propertyInfo => !propertyInfo.GetGetMethod().IsVirtual);

            foreach (var propertyInfo in virtualProperties)
            {
                Message.Write(MessageLocation.Of(targetType),
                                SeverityType.Error,
                                "998",
                                "Property '{0}' in Entity class {1} is not virtual",
                                    propertyInfo.Name, targetType.FullName);
            }
        }
    }
}
