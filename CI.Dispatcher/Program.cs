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

namespace CI.Dispatcher
{
    class Program
    {
        private static string CI_UI_Path => ConfigurationManager.AppSettings["CI_UI_Path"];
        private const int timeBeforeAssumingUINotRunning = 1000;
        private const int timeout = 2000;

        /// <summary>
        /// The purpose of this application is for each time it is executed, dispatch the message to the only running instance of CI.UI (or start it if it isn't running already).
        /// Once the message has been received by CI.UI, execution stops.
        /// </summary>
        static void Main(string[] args)
        {
            Logger.Log("in dispatcher. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));
            Task uiProcess = null;
            try
            {
                var message = ComposeMessage(args);
                if (message != null)
                {
                    var pipe = SetupConnection(out uiProcess);
                    if (pipe != null)
                    {
                        Logger.Log("trying to send message");
                        TrySendMessage(pipe, message);

                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("exception: " + e.Message);

            }
            finally
            {
                Logger.Log("Waiting for UI to finish");
                if (uiProcess != null)
                    uiProcess.Wait();
                Logger.Log("UI finished");
#if DEBUG
                Console.ReadLine();
#endif

            }
        }

        private static string ComposeMessage(string[] args)
        {
            Contract.Requires(args != null);
            Contract.Requires(args.Length > 0);

            return string.Join(ReceivingPipe.Separator, args);
        }

        private static NamedPipeServerStream SetupConnection(out Task uiProcess)
        {
            var pipe = new NamedPipeServerStream(ReceivingPipe.PipeName, PipeDirection.Out);

            // try to make connection, or start the executable in case it's not responding
            Task makeConnectionTask = pipe.WaitForConnectionAsync();
            if (!makeConnectionTask.Wait(timeBeforeAssumingUINotRunning))
            {
                uiProcess = StartCIUI();
                if (uiProcess == null)
                    return null;
            }
            else
                uiProcess = null;

            // try to make connection again, stopping if the potentially started UI ends
            var newMakeConnectionTask = pipe.WaitForConnectionAsync();
            var timeoutTask = Task.Delay(timeout);
            Task firstCompletedTask = Task.WhenAny(newMakeConnectionTask, timeoutTask).Result;
            if (firstCompletedTask == newMakeConnectionTask)
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

        private static Task StartCIUI()
        {


#if DEBUG
            Logger.Log("Starting CI.UI in process");
            return Task.Run(() => UI.Program.Main(Array.Empty<string>()));
#else
            try
            {
                Logger.Log($"Starting CI.UI out of process. Executing '{CI_UI_Path}'");
                var process = Process.Start(new ProcessStartInfo(CI_UI_Path) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });
                return process.WaitForExitAsync();
            }
            catch (Exception e)
            {
                UI.Program.OutputError(e);
                return null;
            }
#endif
        }

        private static void TrySendMessage(NamedPipeServerStream pipe, string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(pipe))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(message);
                    Console.WriteLine("Written message");
                    Logger.Log("written message");
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
                Logger.Log(e.Message);
            }
        }
    }
}
