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
				if(FullyQuantifiedMethodName.TryParse(args[0], out FullyQuantifiedMethodName method))
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

		static ExitCodes TestAll()
		{
			throw new NotImplementedException();
		}
		static ExitCodes Test(FullyQuantifiedMethodName test)
		{
			throw new NotImplementedException();
		}

	}
}
