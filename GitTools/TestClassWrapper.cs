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
        /// Gets the first attribute on the specified method that has a full name in the specified list; or null if none match.
        /// </summary>
        private static CustomAttributeData GetAttributeData(MethodInfo method, IList<string> attributeFullNames)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (attributeFullNames == null) throw new ArgumentNullException(nameof(attributeFullNames));

            return method.CustomAttributes.Where(attribute => GetBaseTypes(attribute.AttributeType).Any(attributeType => attributeFullNames.Contains(attributeType.FullName)))
                                          .FirstOrDefault();
        }
        private static bool HasAttribute(MethodInfo method, IList<string> attributeFullNames)
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
