using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
	public sealed class FullyQuantifiedMethodName
	{
		public static bool TryParse(string input, out FullyQuantifiedMethodName methodName)
		{
			methodName = null;
			if (string.IsNullOrEmpty(input))
			{
				Program.WriteLine("Invalid argument specified");
				return false;
			}

			var separatedInputs = input.Split(new[] { Program.PathFromFullyQuantifiedMethodNameSeparator }, count: 2);
			if (separatedInputs.Length == 1)
			{
				Program.WriteLine("The method name could not be interpreted");
				return false;
			}
			Debug.Assert(separatedInputs.Length == 2);

			var assembly = TryLoadingAssembly(separatedInputs[0]);
			if (assembly == null)
			{
				return false;
			}

			var method = TryResolveName(assembly, fullyQuantifiedMethodName: separatedInputs[1]);
			if (method == null)
			{
				return false;
			}

			methodName = new FullyQuantifiedMethodName(method);
			return true;
		}
		/// <summary> 
		/// Tries to load the assembly from the specified path. 
		/// </summary>
		private static Assembly TryLoadingAssembly(string path)
		{
			if (!File.Exists(path))
			{
				Program.WriteLine("No file found at " + path);
				return null;
			}

			try
			{
				return Assembly.LoadFrom(path);
			}
			catch (Exception ex)
			{
				Program.WriteLine("The assembly could not be loaded");
				Program.WriteLine(ex.Message);
			}
		}

		private static MethodInfo TryResolveName(Assembly assembly, string fullyQuantifiedMethodName)
		{
			if (string.IsNullOrEmpty(fullyQuantifiedMethodName)) throw new ArgumentException(nameof(fullyQuantifiedMethodName));
			if (assembly == null) throw new ArgumentNullException(nameof(assembly));


			assembly.get
			Type t;
			t.FullName
			assembly.GetType()

		}
		private (string TypeName, string MethodName) TrySplit(string fullyQuantifiedMethodName)
		{
			if (string.IsNullOrEmpty(fullyQuantifiedMethodName)) throw new ArgumentException(nameof(fullyQuantifiedMethodName));

			const char typeFromMethodSeparator = '.';

			int splitIndex = fullyQuantifiedMethodName.LastIndexOf(typeFromMethodSeparator);
			if (splitIndex == -1 || splitIndex == 0 || splitIndex >= fullyQuantifiedMethodName.Length - 1)
			{
				return (null, null);
			}

			string typeName = fullyQuantifiedMethodName.Substring(0, splitIndex);
			string methodName = fullyQuantifiedMethodName.Substring(splitIndex);
			return (typeName, methodName);
		}
	}
}
