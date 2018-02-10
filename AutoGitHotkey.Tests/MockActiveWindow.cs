using JBSnorro.AutoGitHotkey;
using JBSnorro.Diagnostics;

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
