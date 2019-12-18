using JBSnorro.GitTools;
using NUnit.Framework;

namespace JBSnorro.AutoGitHotkey.Tests
{
	[TestFixture]
	public class DeterminePathTests
	{
		static DeterminePathTests() => ResolveJBSnorroDll.Resolve();

		[Test]
		public void GetPathFromConfigFile()
		{
			IActiveWindowTitle titleGetter = new MockActiveWindow("ASDE - Microsoft Visual Studio  (Administrator)");
			string path = Program.GetRepositoryPath(titleGetter);
			Assert.IsTrue(DirectoryPathEqualityComparer.Equals(@"D:\ASDE", path));
		}

		[Test]
		public void FailToGetPathFromConfigFile()
		{
			IActiveWindowTitle titleGetter = new MockActiveWindow("Doesn't exist - Microsoft Visual Studio  (Administrator)");

			string path = JBSnorro.AutoGitHotkey.Program.GetRepositoryPath(titleGetter);

			Assert.IsNull(path);
		}
	}
}
