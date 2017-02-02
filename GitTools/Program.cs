using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
	class Program
	{
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
				if (!IsValidTestName(args[0]))
				{
					WriteLine("No test found called " + args[0]);
					return (int)ExitCodes.Abort;
				}
				else
				{
					return (int)Test(args[0]);
				}
			}
			else
			{
				WriteLine("Too many arguments specified");
				return (int)ExitCodes.Abort;
			}
		}

		static ExitCodes TestAll()
		{
			throw new NotImplementedException();
		}
		static ExitCodes Test(string test)
		{
			throw new NotImplementedException();
		}

		static bool IsValidTestName(string testName)
		{
			throw new NotImplementedException();
		}

	}
}
