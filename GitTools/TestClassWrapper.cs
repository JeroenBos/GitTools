using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
    public static class TestClassExtensions
    {
        /// <summary>
        /// Gets whether the type is a type containing tests.
        /// </summary>
        public static bool IsTestType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            return type.CustomAttributes.Any(attribute => GetBaseTypes(attribute.AttributeType).Any(attributeType => TestClassFullNames.Contains(attributeType.FullName)));
        }
        /// <summary>
        /// Runs the initialization method, if any.
        /// </summary>
        /// <param name="instance"> An instance of a type containing tests. </param>
        public static void RunInitializationMethod(object instance)
        {
            GetInitializationMethod(instance)?.Invoke(instance, Array.Empty<object>());
        }
        /// <summary>
        /// Runs the clean up method, if any.
        /// </summary>
        /// <param name="instance"> An instance of a type containing tests. </param>
        public static void RunCleanupMethod(object instance)
        {
            GetCleanupMethod(instance)?.Invoke(instance, Array.Empty<object>());
        }
        /// <summary>
        /// Gets whether the specified method is a test method.
        /// </summary>
        public static bool IsTestMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodAttributeFullNames);
        }
        /// <summary>
        /// Gets whether the specified test method expects the specified exception to be thrown.
        /// </summary>
        public static bool IsExceptionExpected(MethodInfo method, Exception thrownException)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (!IsTestMethod(method)) throw new ArgumentException(nameof(method));

            var attributeData = GetAttributeData(method, TestMethodExpectedExceptionAttributeFullNames.Keys, out var key);
            if (attributeData == null)
                return false;

            var attribute = (Attribute)attributeData.Constructor.Invoke(attributeData.ConstructorArguments.Select(arg => arg.Value).ToArray());
            var verify = TestMethodExpectedExceptionAttributeFullNames[key];
            bool result = verify(attribute, thrownException);

            return result;
        }
        /// <summary>
        /// Gets the first attribute on the specified method that has a full name in the specified list; or null if none match.
        /// </summary>
        private static CustomAttributeData GetAttributeData(MethodInfo method, IReadOnlyCollection<string> attributeFullNames)
        {
            return GetAttributeData(method, attributeFullNames, out var _);
        }
        /// <summary>
        /// Gets the first attribute on the specified method that has a full name in the specified list; or null if none match.
        /// </summary>
        private static CustomAttributeData GetAttributeData(MethodInfo method, IReadOnlyCollection<string> attributeFullNames, out string fullName)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (attributeFullNames == null) throw new ArgumentNullException(nameof(attributeFullNames));

            foreach (var attribute in method.CustomAttributes)
            {
                foreach (var attributeBaseType in GetBaseTypes(attribute.AttributeType))
                {
                    if (attributeFullNames.Contains(attributeBaseType.FullName))
                    {
                        fullName = attributeBaseType.FullName;
                        return attribute;
                    }
                }
            }
            fullName = null;
            return null;
        }
        private static bool HasAttribute(MethodInfo method, IReadOnlyCollection<string> attributeFullNames)
        {
            return GetAttributeData(method, attributeFullNames) != null;
        }
        private static bool IsTestInitializationMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodInitializationAttributeFullNames);
        }
        private static bool IsTestCleanupMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            return HasAttribute(method, TestMethodCleanupAttributeFullNames);
        }
        private static MethodInfo GetInitializationMethod(object testTypeInstance)
        {
            if (testTypeInstance == null) throw new ArgumentNullException(nameof(testTypeInstance));

            return testTypeInstance.GetType()
                                   .GetMethods()
                                   .Where(IsTestInitializationMethod)
                                   .FirstOrDefault();
        }
        private static MethodInfo GetCleanupMethod(object testTypeInstance)
        {
            if (testTypeInstance == null) throw new ArgumentNullException(nameof(testTypeInstance));

            return testTypeInstance.GetType()
                                   .GetMethods()
                                   .Where(IsTestCleanupMethod)
                                   .FirstOrDefault();
        }

        private static List<string> TestClassFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute" };
        private static List<string> TestMethodAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute" };
        private static List<string> TestMethodInitializationAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute" };
        private static List<string> TestMethodCleanupAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute" };
        private static Dictionary<string, Func<Attribute, Exception, bool>> TestMethodExpectedExceptionAttributeFullNames = new Dictionary<string, Func<Attribute, Exception, bool>> { ["Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionBaseAttribute"] = verifyExpectedExceptionBaseAttribute };

        private static bool verifyExpectedExceptionBaseAttribute(Attribute attribute, Exception e)
        {
            var m = attribute.GetType().GetMethod("Verify", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                m.Invoke(attribute, new object[] { e });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<Type> GetBaseTypes(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            yield return type;
            if (type.BaseType != null)
                foreach (var result in GetBaseTypes(type.BaseType))
                    yield return result;
        }
    }
}
