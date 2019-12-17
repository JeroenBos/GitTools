using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.AutoGitHotkey.Tests
{
	class TestProgram
	{
		static TestProgram()
		{
			Assembly.LoadFrom("JBSnorro.CI.dll"); // overrides the name mismatch JBSnorro vs JBSnorro.CI. We must have a different name because otherwise I can't use this on ASDE e.g. because of name clashes
		}
		static void Main(string[] args)
		{
			new DeterminePathTests().GetPathFromConfigFile();
		}
	}
}
