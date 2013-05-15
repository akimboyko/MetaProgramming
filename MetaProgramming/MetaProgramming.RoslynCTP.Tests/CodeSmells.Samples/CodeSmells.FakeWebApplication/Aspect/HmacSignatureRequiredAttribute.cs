using System;
using System.Linq;
using System.Reflection;
using PostSharp;
using PostSharp.Constraints;
using PostSharp.Extensibility;

namespace CodeSmells.FakeWebApplication.Aspect
{
    [Serializable]
    [MulticastAttributeUsage(MulticastTargets.Class)]
    public class HmacSignatureRequiredAttribute : ScalarConstraint
    {
        private readonly string[] _httpVerbs;

        public HmacSignatureRequiredAttribute(string[] httpVerbs)
        {
            _httpVerbs = httpVerbs;
        }

        public override void ValidateCode(object target)
        {
            var targetType = (Type)target;

            var httpVerbsMethods = targetType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(methodInfo => _httpVerbs.Contains(methodInfo.Name))
                .Where(
                    methodInfo =>
                    !(methodInfo.GetParameters().First().ParameterType == typeof (int)
                      && methodInfo.GetParameters().First().Name == "userId")
                    ||
                    !(methodInfo.GetParameters().Last().ParameterType == typeof (string)
                      && methodInfo.GetParameters().Last().Name == "hmacSignature"));

            foreach (var methodInfo in httpVerbsMethods)
            {
                Message.Write(MessageLocation.Of(targetType),
                                SeverityType.Error,
                                "997",
                                "Method '{0}' in ApiController class {1} required to have both userId/hmacSignature",
                                    methodInfo.Name, targetType.FullName);
            }
        }
    }
}