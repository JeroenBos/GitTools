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
        private static CancellableByDisposalCancellationTokenSource inProcessMessageProcesserCancellationTokenSource;
        private static bool inProcessMessageProcesserIsRunning => inProcessMessageProcesserCancellationTokenSource != null;
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
            IDisposable disposable = null;
            Logger.Log("in dispatcher. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));
            try
            {
                if (args.Length > 0 && args[0] == START_UI_ARG)
                {
#if DEBUG
                    disposable = StartCIUI(inProcess: true);
#else
                    disposable = StartCIUI(inProcess: false);
#endif
                    args = args.Skip(1).ToArray();
                }

                var message = ComposeMessage(args);
                if (message != null)
                {
                    TrySendMessage(message);
                }
            }
            catch (Exception e)
            {
                Logger.Log("exception: " + e.Message);
            }
            finally
            {
                if (disposable != null)
                {
                    Logger.Log("Disposing started UI");
                    disposable.Dispose();
                }
#if DEBUG
                Console.ReadLine();
#endif
            }
        }

        private static string ComposeMessage(string[] args)
        {
            Contract.Requires(args != null);
            Contract.Requires(args.Length > 0);

            return string.Join(CIReceivingPipe.PipeMessageSeparator, args);
        }

        private static NamedPipeServerStream TrySetupConnection()
        {
            if (!inProcessMessageProcesserIsRunning && Process.GetProcessesByName("CI.UI").Length == 0)
            {
                Logger.Log("The receiving end of the pipe is not running");
                return null;
            }

            var pipe = new NamedPipeServerStream(CIReceivingPipe.PipeName, PipeDirection.Out);
            // try to make connection, or start the executable in case it's not responding
            Task makeConnectionTask = pipe.WaitForConnectionAsync();
            Task timeoutTask = Task.Delay(timeout);
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

        internal static IDisposable StartCIUI(bool inProcess = false)
        {
            if (inProcess)
            {
                if (!inProcessMessageProcesserIsRunning)
                {
                    inProcessMessageProcesserCancellationTokenSource = new CancellableByDisposalCancellationTokenSource(() => inProcessMessageProcesserCancellationTokenSource == null);
                    Logger.Log("Starting CI.UI in process");
                    Task.Run(() => Program.Start(inProcessMessageProcesserCancellationTokenSource.Token))
                        .ContinueWith(t => inProcessMessageProcesserCancellationTokenSource = null);
                }
                return inProcessMessageProcesserCancellationTokenSource;
            }
            else if (Process.GetProcessesByName("CI.UI").Length != 0)
            {
                return null;
            }
            else
            {
                Logger.Log($"Starting CI.UI out of process. Executing '{CI_UI_Path}'");
                return Process.Start(CI_UI_Path);
            }
        }
        private sealed class CancellableByDisposalCancellationTokenSource : CancellationTokenSource
        {
            private readonly Func<bool> isCanceled;
            public CancellableByDisposalCancellationTokenSource(Func<bool> isCanceled = null)
            {
                this.isCanceled = isCanceled;
            }
            protected override void Dispose(bool disposing)
            {
                Logger.Log("Cancelling UI");
                this.Cancel();
                base.Dispose(disposing);
                if (isCanceled != null)
                {
                    while (!isCanceled())
                    {
                        Thread.Sleep(1);
                    }
                }
            }
        }

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
