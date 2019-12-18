using JBSnorro.Diagnostics;
using JBSnorro.GitTools;

namespace JBSnorro.AutoGitHotkey.Tests
{
	class MockActiveWindow : IActiveWindowTitle
	{
		public string Title { get; }

		public MockActiveWindow(string title)
		{
			Contract.Requires(!string.IsNullOrEmpty(title));

			this.Title = title;
		}

		string IActiveWindowTitle.GetActiveWindowTitle() => Title;
	}
}
