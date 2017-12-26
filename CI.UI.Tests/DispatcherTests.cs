using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JBSnorro;
using JBSnorro.Diagnostics;
using System.Threading;

namespace CI.UI.Tests
{
    [TestClass]
    public class DispatcherTests
    {
        [TestMethod]
        public void TestDispatch()
        {
            Dispatcher.StartCIUI(inProcess: true);
            {
                bool messageSent = Dispatcher.TrySendMessage("hi");

                Assert.IsTrue(messageSent);
            }
        }
    }
}
