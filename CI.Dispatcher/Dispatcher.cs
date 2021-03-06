﻿using CI.UI;
using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using JBSnorro.GitTools.CI;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CI
{
	internal class Dispatcher
	{
		private static RunnableTaskCancellableByDisposal inProcessUI;
		private static bool inProcessUIIsRunning => inProcessUI != null;

		private static string CI_UI_Path => ConfigurationManager.AppSettings["CI_UI_Path"] ?? throw new AppSettingNotFoundException("CI_UI_Path");
		/// <summary>
		/// Gets the timeout in milliseconds after which the dispatch receiver may be presumed absent.
		/// </summary>
		private static readonly int timeout = readTimeoutFromSettings();
		private static int readTimeoutFromSettings()
		{
			const string key = "timeout_ms";
			string timeout_string = ConfigurationManager.AppSettings[key] ?? throw new AppSettingNotFoundException(key);
			if (int.TryParse(timeout_string, out int result))
				return result;
			else
				throw new ContractException($"Invalid 'app.config': '{key}' ({timeout_string}) is invalid");

		}
		private const string START_UI_ARG = "UI";

		/// <summary>
		/// The purpose of this application is for each time it is executed, dispatch the message to the only running instance of CI.UI (and starts it if it isn't already).
		/// Once the message has been sent to CI.UI, execution stops. 
		/// </summary>
		/// <param name="args"> The arguments (except for possibly the first if it is `UI`) are piped to the CI. An example is <code>dir\\GitTools.sln" $hash</code></param>
		internal static void Main(string[] args)
		{
			using (var cts = new CancellationTokenSource())
			{
				IDisposable ui = null;
				Logger.Log("in dispatcher. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));

				if (IsCIDisabled(args))
				{
					Logger.Log("Error. Did not reach CI. It may be disabled");
					return;
				}

				try
				{
					if (args.Length > 0 && args[0] == START_UI_ARG)
					{
#if DEBUG
						ui = StartCIUIInProcess();
#else
                        ui = StartCIUIOutOfProcess();
#endif
						args = args.Skip(1).ToArray();
					}
					var message = ComposeMessage(args);
					if (message != null)
					{
						TrySendMessage(message, cts.Token);
					}
#if DEBUG
					Console.ReadLine();
					((RunnableTaskCancellableByDisposal)ui).Cancel();
#endif
				}
				catch (Exception e)
				{
					Logger.Log("exception: " + e.Message);
#if DEBUG
					Console.ReadLine();
#endif
				}
				finally
				{
					cts.Cancel(); // cancels NamedPipeServerStream.WaitForConnectionAsync
					if (ui != null)
					{
						Logger.Log("Disposing started UI");
						ui.Dispose();
					}
				}
			}
		}

		public static string ComposeMessage(params string[] args)
		{
			Contract.Requires(args != null);
			Contract.Requires(args.Length > 0, "Insufficient arguments specified");

			return string.Join(CIReceivingPipe.PipeMessageSeparator, args);
		}

		/// <summary>
		/// Reads from the main arguments whether the CI is disabled. So this method knows about the contents of the arguments, whereas in the rest of the dispatcher they're simply concatenated and passed on.
		/// I chose this because reading them (after having been passed on) happens asynchronously and thus the CI disabled flag may be removed by the time it is read.
		/// </summary>
		private static bool IsCIDisabled(string[] args)
		{
			try
			{
				if (args.Length <= 2)
					return false; // there's no file specified as argument, so let's just propagate the args to the CI

				string file = args[2];
				string solutionDirectory = Path.GetDirectoryName(file);
				Contract.Assert(Directory.Exists(solutionDirectory), $"Directory '{solutionDirectory}' does not exist");

				return TemporaryCIDisabler.IsDisabled(solutionDirectory);
			}
			catch (Exception e)
			{
				Logger.Log(e.Message);
				return true;
			}
		}
		private static NamedPipeServerStream TrySetupConnection(CancellationToken cancellationToken)
		{
			if (!inProcessUIIsRunning && Process.GetProcessesByName("CI.UI").Length == 0)
			{
				Logger.Log("The receiving end of the pipe is not running");
				return null;
			}

			var pipe = new NamedPipeServerStream(CIReceivingPipe.GetPipeName(), PipeDirection.Out);
			// try to make connection, or start the executable in case it's not responding
			Task makeConnectionTask = pipe.WaitForConnectionAsync(cancellationToken);
			if (makeConnectionTask.Wait(timeout))
			{
				Logger.Log("Found listener");
				return pipe; //connection made
			}
			else
			{
				Logger.Log("Dispatching the message to CI.UI timed out");
				return null;
			}
		}
		/// <summary>
		/// Starts the UI in process with the specified icon.
		/// </summary>
		internal static IDisposable StartCIUI(NotificationIcon icon)
		{
			Contract.Requires(icon != null);
			Contract.Requires(!inProcessUIIsRunning, "The UI is already running in process");

			Logger.Log("Starting CI.UI in process");
			return new RunnableTaskCancellableByDisposal(token => UI.Program.Start(icon, token));
		}
		/// <summary>
		/// Starts the UI with a new icon.
		/// </summary>
		/// <param name="inProcess"> Indicates whether the ui should be started in the current process (true) or in a new process process (false). </param>
		internal static IDisposable StartCIUIInProcess()
		{
			Contract.Requires(!inProcessUIIsRunning, "The UI is already running in process");

			Logger.Log("Starting CI.UI in process");
			return new RunnableTaskCancellableByDisposal(UI.Program.Start);
		}
		private static IDisposable StartCIUIOutOfProcess()
		{
			if (Process.GetProcessesByName("CI.UI").Length != 0)
			{
				return null; //already running
			}
			else
			{
				Logger.Log($"Starting CI.UI out of process. Executing '{CI_UI_Path}'");
				return Process.Start(CI_UI_Path);
			}
		}
		private sealed class RunnableTaskCancellableByDisposal : CancellationTokenSource
		{
			private readonly Task task;
			public RunnableTaskCancellableByDisposal(Action<CancellationToken> cancellableAction)
			{
				Contract.Requires(cancellableAction != null);

				this.task = Task.Run(() => cancellableAction(this.Token))
								.ContinueWith(t => Logger.Log("Unhandled exception occurred: " + t.Exception.InnerException.Message), TaskContinuationOptions.OnlyOnFaulted);
				inProcessUI = this;
			}

			public void Wait()
			{
				try
				{
					this.task.Wait(this.Token);
				}
				catch (AggregateException ae) when (ae.InnerException is TaskCanceledException) { }
			}
			protected override void Dispose(bool disposing)
			{
				Logger.Log("Cancelling CI.UI");
				try
				{
					this.Cancel();
				}
				catch (AggregateException e) when (e.InnerException is ObjectDisposedException)
				{
					return;
				}
				finally
				{
					try
					{
						this.task.Wait();
					}
					catch (AggregateException ae) when (ae.InnerException is TaskCanceledException) { }
					catch (Exception e) { Logger.Log("Unhandled error: " + e.Message); }

					base.Dispose(disposing);
					inProcessUI = null;
					Logger.Log("Cancelled CI.UI");
				}
			}
		}
		/// <summary>
		/// Sends the specified message and immediately closes the connection again.
		/// </summary>
		/// <param name="message"> By default, the message is the sln file, possibly with a hash of a particular commit. 
		/// Prepend with <see cref="START_UI_ARG"/> to start the UI. All separated by <see cref="CIReceivingPipe.PipeMessageSeparator"/>. 
		/// The last argument can also be <see cref="UI.Program.DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT"/>. </param>
		/// <returns> whether the message was sent. </returns>
		internal static bool TrySendMessage(string message)
		{
			using (CancellationTokenSource cts = new CancellationTokenSource())
			{
				var result = TrySendMessage(message, cts.Token);
				cts.Cancel(); // cancels NamedPipeServerStream.WaitForConnectionAsync
				return result;
			}
		}
		private static bool TrySendMessage(string message, CancellationToken cancellationToken)
		{
			using (var pipe = TrySetupConnection(cancellationToken))
			{
				if (pipe != null)
				{
					Logger.Log("trying to send message");
					return TrySendMessage(pipe, message);
				}
				return false;
			}
		}
		private static bool TrySendMessage(NamedPipeServerStream pipe, string message)
		{
			try
			{
				using (StreamWriter writer = new StreamWriter(pipe))
				{
					writer.AutoFlush = true;
					writer.WriteLine(message);
					Console.WriteLine("Written message");
					Logger.Log("written message");
					return true;
				}
			}
			catch (IOException e)
			{
				Console.WriteLine("ERROR: {0}", e.Message);
				Logger.Log(e.Message);
				return false;
			}
		}
	}
}
