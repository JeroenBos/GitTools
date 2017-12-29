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
using JBSnorro.GitTools.CI;

namespace CI.UI.Tests
{
    [TestClass]
    public class DispatcherTests
    {
        [TestMethod]
        public void TestDispatch()
        {
            using (Dispatcher.StartCIUI(inProcess: true))
            {
                bool messageSent = Dispatcher.TrySendMessage("hi");

                Assert.IsTrue(messageSent);
            }
        }
        [TestMethod, Timeout(1000)]
        public void TestDispatchReceipt()
        {
            Logger.Log("TestDispatchReceipt started");
            const string testMessage = "hi";

            using (Dispatcher.StartCIUI(inProcess: true))
            {
                string receivedMessage = null;
                ReceivingPipe.OnHandledMessage += (sender, message) => receivedMessage = message;

                Dispatcher.TrySendMessage(testMessage);

                Logger.Log("finished sending");
                while (receivedMessage == null) //canceled by TimeoutAttribute
                {
                    Thread.Sleep(1);
                }
                Assert.AreEqual(testMessage, receivedMessage);
                Logger.Log("finished assertion");
            }
            Logger.Log("finished disposing");
        }
    }
}
