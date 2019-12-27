using JBSnorro.Diagnostics;
using JBSnorro.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace JBSnorro.GitTools
{
	public static class TestClassExtensions
	{
		static TestClassExtensions()
		{
			isRunningFromUnitTest = new AppDomainField<bool>(appDomain => appDomain.GetAssemblies().Select(a => a.FullName).Any(assemblyName => TestingAssemblies.Any(assemblyName.StartsWith)), discardValueOnAssemblyAdded: true);
		}
		private static readonly AppDomainField<bool> isRunningFromUnitTest;
		/// <summary>
		/// Gets whether the code is running from a unit test.
		/// </summary>
		public static bool IsRunningFromUnitTest => isRunningFromUnitTest.Value;

		/// <summary>
		/// Gets the name of the currently running test method; or null if no test method is running.
		/// </summary>
		public static string RunningTestMethodName
		{
			get
			{
				foreach (var stackMethod in new StackTrace().GetFrames().Reverse().Select(frame => frame.GetMethod()).OfType<MethodInfo>())
				{
					if (IsTestMethod(stackMethod))
					{
						return stackMethod.Name;
					}
				}
				return null;
			}
		}
		/// <summary>
		/// Gets whether the type is a type containing tests.
		/// </summary>
		public static bool IsTestType(Type type)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));

			return type.CustomAttributes.Any(attribute => attribute.AttributeType.GetBaseTypesAndSelf().Any(attributeType => TestClassFullNames.Contains(attributeType.FullName)));
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

			return method.HasAttribute(TestMethodAttributeFullNames) && !method.HasAttribute(TestMethodIgnoreAttributeFullNames);
		}
		/// <summary>
		/// Gets all test methods in the specified assembly.
		/// </summary>
		public static IEnumerable<MethodInfo> GetTestMethods(string assemblyPath)
		{
			Contract.Requires(!string.IsNullOrEmpty(assemblyPath));

			return Assembly.LoadFrom(assemblyPath)
						   .GetTypes()
						   .Where(IsTestType)
						   .SelectMany(GetTestMethods);
		}
		/// <summary>
		/// Gets all test methods in the specified type.
		/// </summary>
		/// <param name="testType"></param>
		/// <returns></returns>
		public static IEnumerable<MethodInfo> GetTestMethods(Type testType)
		{
			Contract.Requires(testType != null);
			Contract.Requires(IsTestType(testType));

			return testType.GetMethods().Where(IsTestMethod);
		}
		/// <summary>
		/// Gets whether the specified test method expects the specified exception to be thrown.
		/// </summary>
		public static bool IsExceptionExpected(MethodInfo method, Exception thrownException)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (!IsTestMethod(method)) throw new ArgumentException(nameof(method));


			var attribute = method.GetAttribute(TestMethodExpectedExceptionAttributeFullNames.Keys, out var key);
			if (attribute == null)
				return false;

			var verify = TestMethodExpectedExceptionAttributeFullNames[key];
			bool result = verify(attribute, thrownException);

			return result;
		}
		/// <summary>
		/// If the specified method has a timeout attribute, its timeout value in ms is returned; otherwise null. 
		/// </summary>
		public static int? GetTestMethodTimeout(MethodInfo method)
		{
			Contract.Requires(method != null);
			Contract.Requires(IsTestMethod(method));

			var attribute = method.GetAttribute(TestMethodTimeoutAttribute.Keys, out string fullName);
			if (attribute == null)
				return null;

			if (fullName == "Microsoft.VisualStudio.TestTools.UnitTesting.TimeoutAttribute")
			{
				return (int)attribute.GetType().GetProperty("Timeout").GetValue(attribute);
			}
			const string nunitAttributeName = "NUnit.Framework.TimeoutAttribute";
			if (fullName == nunitAttributeName)
			{
				return (int)method.GetCustomAttributesData()
								  .Where(m => m.AttributeType.FullName == nunitAttributeName)
								  .First().ConstructorArguments
										  .First()
										  .Value;
			}
			throw new NotImplementedException($"Getting the time out from '{fullName}' is not implemented");
		}


		private static bool IsTestInitializationMethod(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			return method.HasAttribute(TestMethodInitializationAttributeFullNames);
		}
		private static bool IsTestCleanupMethod(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			return method.HasAttribute(TestMethodCleanupAttributeFullNames);
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

		private static List<string> TestClassFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute", "NUnit.Framework.TestFixtureAttribute" };
		private static List<string> TestMethodAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute", "NUnit.Framework.TestAttribute" };
		private static List<string> TestMethodInitializationAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute", "NUnit.Framework.SetUpAttribute" }; // TODO: implement NUnit.Framework.SetUpFixtureAttribute
		private static List<string> TestMethodCleanupAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute", "NUnit.Framework.TearDownAttribute" };
		private static List<string> TestMethodIgnoreAttributeFullNames = new List<string> { "Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute", "NUnit.Framework.IgnoreAttribute" };
		private static List<string> TestingAssemblies = new List<string> { "Microsoft.VisualStudio.TestPlatform", "nunit.framework" };
		private static Dictionary<string, Func<Attribute, Exception, bool>> TestMethodExpectedExceptionAttributeFullNames = new Dictionary<string, Func<Attribute, Exception, bool>> { ["Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedExceptionBaseAttribute"] = verifyExpectedExceptionBaseAttribute };
		private static Dictionary<string, Func<CustomAttributeData, int>> TestMethodTimeoutAttribute = new Dictionary<string, Func<CustomAttributeData, int>> { ["Microsoft.VisualStudio.TestTools.UnitTesting.TimeoutAttribute"] = getTimoutValue, ["NUnit.Framework.TimeoutAttribute"] = getTimoutValue };

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
		private static int getTimoutValue(CustomAttributeData attributeData)
		{
			Contract.Requires(attributeData != null);
			Contract.Requires(attributeData.ConstructorArguments.Count == 1);
			var argType = attributeData.ConstructorArguments[0].ArgumentType;
			Contract.Requires(argType == typeof(int) || argType.FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.TestTimeout");

			try
			{
				return (int)attributeData.ConstructorArguments[0].Value;
			}
			catch (InvalidCastException)
			{
				return int.MaxValue; // = Microsoft.VisualStudio.TestTools.UnitTesting.TestTimeout.Infinite
			}
		}
	}
}
