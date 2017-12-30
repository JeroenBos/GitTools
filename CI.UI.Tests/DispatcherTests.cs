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
using NUnit.Framework;

namespace CI.UI.Tests
{
    [TestFixture]
    public class DispatcherTests
    {

        private static string ComposeDummyWorkMessage(int timeout_ms)
        {
            return $"{Program.TEST_ARGUMENT}{CIReceivingPipe.PipeMessageSeparator}{timeout_ms}";
        }
        [SetUp]
        public void ResetPrefix()
        {
            Logger.Prefix = "";
        }
        [Test, RequiresThread]
        public void TestDispatch()
        {
            using (Dispatcher.StartCIUI(inProcess: true))
            {
                bool messageSent = Dispatcher.TrySendMessage("hi");

                Assert.IsTrue(messageSent);
            }
        }
        [Test, Timeout(1000)]
        public void TestDispatchReceipt()
        {
            const string testMessage = "hi";
            string receivedMessage = null;

            using (Dispatcher.StartCIUI(inProcess: true))
            {
                ReceivingPipe.OnHandledMessage += (sender, message) => receivedMessage = message;

                Dispatcher.TrySendMessage(testMessage);

                while (receivedMessage == null) //canceled by TimeoutAttribute
                {
                    Thread.Sleep(1);
                }
            }

            // the difference between asserting inside or outside of the using statement is that inside these assertions will only evaluated after all background threads have finished,
            // which is precisely what we're not testing here. I assume this is a NUnit thing, but in case it isn't, this may explain why I didn't understand everything
            Assert.AreEqual(testMessage, receivedMessage);
        }
        [Test, Timeout(1000)]
        public void TestReceiptMultipleMessages()
        {
            Logger.Prefix = "Test - ";
            const int timeout_ms = 500;
            string message = ComposeDummyWorkMessage(timeout_ms);
            int receivedMessageCount = 0;
            int handledMessageCount = 0;
            ReceivingPipe.OnReceivedMessage += (sender, e) => receivedMessageCount++;
            ReceivingPipe.OnHandledMessage += (sender, e) => handledMessageCount++;

            using (Dispatcher.StartCIUI(inProcess: true))
            {
                //Act
                Dispatcher.TrySendMessage(message);
                while (receivedMessageCount != 1)
                {
                    Thread.Sleep(10);
                }
                Logger.Log("received message count became 1. Handled: " + handledMessageCount);
                Dispatcher.TrySendMessage(message);
                while (receivedMessageCount != 2)
                {
                    Thread.Sleep(10);
                }
                Logger.Log("received message count became 2. Handled: " + handledMessageCount);
            }

            Assert.AreEqual(2, receivedMessageCount);
            Assert.AreEqual(0, handledMessageCount);
        }
        [Test, Timeout(1000)]
        public void MessagesAreHandledAfterCancellation()
        {
            const string test_message = "hi";
            string handledMessage = null;
            ReceivingPipe.OnHandledMessage += (sender, message) => handledMessage = message;
            using (Dispatcher.StartCIUI(inProcess: true))
            {
                Dispatcher.TrySendMessage(ComposeDummyWorkMessage(timeout_ms: int.MaxValue));           // send message

                while (Program.Icon_TESTING == null)
                {
                    Thread.Sleep(10);
                }

                Thread.Sleep(100);
                Program.Icon_TESTING.RequestCancellation();                                             // which is then cancelled.

                Contract.Assert(handledMessage == null);
                Dispatcher.TrySendMessage(test_message);                                                // Then send another message
                while (handledMessage == null)
                {
                    Thread.Sleep(10);
                }
            }

            Assert.AreEqual(test_message, handledMessage);                                              // which then should be handled
        }
    }
}
