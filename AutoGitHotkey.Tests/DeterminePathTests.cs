using JBSnorro.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.AutoGitHotkey.Tests
{
    [TestClass]
    public class DeterminePathTests
    {
        [TestMethod]
        public void GetPathFromConfigFile()
        {
            IActiveWindowTitle titleGetter = new MockActiveWindow("ASDE - Microsoft Visual Studio (Administrator)");
            string path = Program.GetRepositoryPath(titleGetter);
            Assert.IsTrue(DirectoryPathEqualityComparer.Equals(@"D:\ASDE", path));
        }

        [TestMethod]
        public void FailToGetPathFromConfigFile()
        {
            IActiveWindowTitle titleGetter = new MockActiveWindow("Doesn't exist - Microsoft Visual Studio (Administrator)");

            string path = Program.GetRepositoryPath(titleGetter);

            Assert.IsNull(path);
        }
    }
}
