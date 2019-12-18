namespace JBSnorro.AutoGitHotkey.Tests
{
	class TestProgram
	{
		static void Main(string[] args)
		{
			ResolveJBSnorroDll.Resolve();
			new DeterminePathTests().GetPathFromConfigFile();
		}
	}
}
