using CI.UI;
using System.Linq;
using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools.CI;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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
        /// The purpose of this application is for each time it is executed, dispatch the message to the only running instance of CI.UI.
        /// Once the message has been sent to CI.UI, execution stops.
        /// </summary>
        internal static void Main(string[] args)
        {
            IDisposable ui = null;
            Logger.Log("in dispatcher. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));
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
                    TrySendMessage(message);
                }
#if DEBUG
                Console.ReadLine();
                ((RunnableTaskCancellableByDisposal)ui).Cancel();
#endif
            }
            catch (Exception e)
            {
                Logger.Log("exception: " + e.Message);
            }
            finally
            {
                if (ui != null)
                {
                    Logger.Log("Disposing started UI");
                    ui.Dispose();
                }
            }
        }

        public static string ComposeMessage(params string[] args)
        {
            Contract.Requires(args != null);
            Contract.Requires(args.Length > 0);

            return string.Join(CIReceivingPipe.PipeMessageSeparator, args);
        }

        private static NamedPipeServerStream TrySetupConnection()
        {
            if (!inProcessUIIsRunning && Process.GetProcessesByName("CI.UI").Length == 0)
            {
                Logger.Log("The receiving end of the pipe is not running");
                return null;
            }

            var pipe = new NamedPipeServerStream(CIReceivingPipe.GetPipeName(), PipeDirection.Out);
            // try to make connection, or start the executable in case it's not responding
            Task makeConnectionTask = pipe.WaitForConnectionAsync();
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
            return new RunnableTaskCancellableByDisposal(token => Program.Start(icon, token));
        }
        /// <summary>
        /// Starts the UI with a new icon.
        /// </summary>
        /// <param name="inProcess"> Indicates whether the ui should be started in the current process (true) or in a new process process (false). </param>
        internal static IDisposable StartCIUIInProcess()
        {
            Contract.Requires(!inProcessUIIsRunning, "The UI is already running in process");

            Logger.Log("Starting CI.UI in process");
            return new RunnableTaskCancellableByDisposal(Program.Start);
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
        /// By default, the message is the sln file, possibly with a hash of a particular commit. Prepend with <see cref="START_UI_ARG"/> to start the UI. All separated by <see cref="CIReceivingPipe.PipeMessageSeparator"/>.
        /// </summary>
        internal static bool TrySendMessage(string message)
        {
            using (var pipe = TrySetupConnection())
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
