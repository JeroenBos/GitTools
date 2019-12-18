using System;

namespace JBSnorro.GitTools
{
	class Program
	{
		/// <summary>
		/// This char separates the path of the file containing the test method from the fully quantified name of the test method. 
		/// </summary>
		public static readonly char PathFromFullyQuantifiedMethodNameSeparator = '+';

		/// <summary> 
		/// Writes the specified message to the console. 
		/// </summary>
		/// <param name="message"></param>
		static void WriteLine(string message)
		{
			if (string.IsNullOrEmpty(message)) throw new ArgumentException(nameof(message));

			throw new NotImplementedException();
		}
		static int Main(string[] args)
		{
			// if no argument is specified, the script tests all methods identified by TestMethod
			// if a single argument is specified, it must be a path + method quantification of a method 

			if (args == null || args.Length == 0)
			{
				return (int)TestAll();
			}
			else if (args.Length == 1)
			{
				if (FullyQuantifiedMethodName.TryParse(args[0], out FullyQuantifiedMethodName method))
				{
					return (int)Test(method);
				}
				else
				{
					WriteLine("No test found called " + args[0]);
					return (int)ExitCodes.Abort;
				}
			}
			else
			{
				WriteLine("Too many arguments specified");
				return (int)ExitCodes.Abort;
			}
		}
		/// <summary>
		/// Tests all methods indentified by TestMethodAttribute.
		/// </summary>
		/// <returns> whether all tests passed </returns>
		static ExitCodes TestAll()
		{
			throw new NotImplementedException();
		}
		/// <summary>
		/// Tests the specified method.
		/// </summary>
		/// <param name="method"> The fully quantified name of the method to test. </param>
		/// <returns> whether the test succeeded. </returns>
		static ExitCodes Test(FullyQuantifiedMethodName method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			throw new NotImplementedException();
		}

	}
}
