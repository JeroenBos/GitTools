using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using JBSnorro.GitTools.CI;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CI.UI.Tests
{
	[TestFixture]
	public class DispatcherTests
	{
		public static bool IsDebugging => new StackFrame(2).GetMethod().Name == "Main";
		/// <summary>
		/// Runs the CI.UI program on the tests in <see cref="__TESTS__"/>. 
		/// </summary>
		/// <remarks> Requires Microsoft.Build dlls. </remarks>
		[Test]
		public void RunTests()
		{
			const int expectedSuccessfulTests = 1;
			Task waitForHandledMessage = WaitForHandledMessageAsync();
			using (NotificationIcon icon = new NotificationIcon())
			using (Dispatcher.StartCIUI(icon))
			{
				string slnPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Tests.sln"));
				string hash = new GitCommandLine(Path.GetDirectoryName(slnPath)).GetCurrentCommitHash();
				Contract.Assert<NotImplementedException>(!slnPath.Contains(' '));

				//Act
				bool messageSent = Dispatcher.TrySendMessage(Dispatcher.ComposeMessage(slnPath, hash, Program.DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT));
				Contract.Assert(messageSent);

				if (!waitForHandledMessage.Wait(IsDebugging ? -1 : 5000))
					throw new TimeoutException("The message was never handled");

				Contract.Assert(icon.Text == $"Done. {expectedSuccessfulTests} tests successful");
			}
		}
		private static Task WaitForHandledMessageAsync()
		{
			ManualResetEvent @event = new ManualResetEvent(false);
			ReceivingPipe.OnHandledMessage += (sender, message) => { try { @event.Set(); } catch (ObjectDisposedException) { } };
			Task task = Task.Run(() => @event.WaitOne())
							.ContinueWith(t => @event.Dispose());
			return task;
		}
		private static string ComposeDummyWorkMessage(int timeout_ms)
		{
			return $"{Program.TEST_ARGUMENT}{CIReceivingPipe.PipeMessageSeparator}{timeout_ms}";
		}

		[Test]
		public void TestDispatch()
		{
			Logger.Log("");
			bool messageSent = false;
			bool madeit = false;
			try
			{
				using (Dispatcher.StartCIUIInProcess())
				{
					madeit = true;
					messageSent = Dispatcher.TrySendMessage("hi");
				}
			}
			finally
			{
				if (madeit)
					Logger.Log("Made it");
				else
					Logger.Log("Didn't make it");
			}

			Assert.IsTrue(messageSent);
		}
		[Test, Timeout(1000)]
		public void TestDispatchReceipt()
		{
			const string testMessage = "hi";
			string receivedMessage = null;

			using (Dispatcher.StartCIUIInProcess())
			{
				ReceivingPipe.OnHandledMessage += (sender, message) => receivedMessage = message;

				Dispatcher.TrySendMessage(testMessage);

				while (receivedMessage == null) //canceled by TimeoutAttribute
				{
					Thread.Sleep(1);
				}

				Logger.Log("TestDispatchReceipt received message");
			}

			// the difference between asserting inside or outside of the using statement is that inside these assertions will only evaluated after all background threads have finished,
			// which is precisely what we're not testing here. I assume this is a NUnit thing, but in case it isn't, this may explain why I didn't understand everything
			Assert.AreEqual(testMessage, receivedMessage);
		}
		[Test, Timeout(1000)]
		public void TestReceiptMultipleMessages()
		{
			const int timeout_ms = 500;
			string message = ComposeDummyWorkMessage(timeout_ms);
			int receivedMessageCount = 0;
			int handledMessageCount = 0;
			ReceivingPipe.OnReceivedMessage += (sender, e) => receivedMessageCount++;
			ReceivingPipe.OnHandledMessage += (sender, e) => handledMessageCount++;

			using (Dispatcher.StartCIUIInProcess())
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
			using (NotificationIcon icon = new NotificationIcon())
			using (Dispatcher.StartCIUI(icon))
			{
				Dispatcher.TrySendMessage(ComposeDummyWorkMessage(timeout_ms: int.MaxValue));           // send message

				while (!icon.HasCancellationRequestedHandler)                                           // wait for it to be cancellable
				{
					Thread.Sleep(10);
				}

				icon.RequestCancellation();                                                             // then cancel it

				Contract.Assert(handledMessage == null);
				Dispatcher.TrySendMessage(test_message);                                                // then send another message
				while (handledMessage == null)
				{
					Thread.Sleep(10);
				}
			}

			Assert.AreEqual(test_message, handledMessage);                                              // which then should be handled
		}
	}
}
