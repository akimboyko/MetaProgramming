using System;
using System.Linq;
using System.Reflection;
using PostSharp;
using PostSharp.Constraints;
using PostSharp.Extensibility;

namespace CodeSmells.FakeDataAccessLibrary.Aspect
{
    [Serializable]
    [MulticastAttributeUsage(MulticastTargets.Property)]
    public class VirtualKeywordRequiredForInstancePropertiesAttribute : ScalarConstraint
    {
        // Validation happens only at post-compile phase
        public override void ValidateCode(object target)
        {
            var propertyInfo = (PropertyInfo)target;
            var targetType = propertyInfo.DeclaringType;

            if (targetType != null)
            {
                var virtualInstanceProperty = targetType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(propInfo => propInfo.CanRead & propertyInfo.CanWrite)
                    .Where(propInfo => propInfo.GetGetMethod() != null)
                        .Where(propInfo => propInfo.GetGetMethod().IsVirtual)
                    .Where(propInfo => propInfo.GetSetMethod() != null)
                        .Where(propInfo => propInfo.GetSetMethod().IsVirtual)
                    .SingleOrDefault(propInfo => propInfo == propertyInfo);

                if (virtualInstanceProperty == null)
                {
                    Message.Write(MessageLocation.Of(targetType),
                                  SeverityType.Error,
                                  "998",
                                  "Property '{0}' in Entity class {1} show be public, virtual with both getter and setter",
                                  propertyInfo.Name, targetType.FullName);
                }
            }
        }
    }
}
