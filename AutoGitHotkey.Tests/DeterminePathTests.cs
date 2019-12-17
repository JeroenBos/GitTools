using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JBSnorro.AutoGitHotkey;

namespace JBSnorro.AutoGitHotkey.Tests
{
    [TestFixture]
    public class DeterminePathTests
    {
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
