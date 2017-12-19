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
            Logger.Log("in main. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));
            try
            {
                var message = ComposeMessage(args);
                if (message != null)
                {
                    var pipe = SetupConnection();
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

        private static NamedPipeServerStream SetupConnection()
        {
            var pipe = new NamedPipeServerStream(ReceivingPipe.PipeName, PipeDirection.Out);

            // try to make connection, or start the executable in case it's not responding
            Task makeConnectionTask = pipe.WaitForConnectionAsync();
            Task ciProcessTask;
            if (!makeConnectionTask.Wait(timeBeforeAssumingUINotRunning))
            {
                ciProcessTask = StartCIUI();
                if (ciProcessTask == null)
                    return null;
            }
            else
                ciProcessTask = Task.Delay(-1);

            // try to make connection again, stopping if the potentially started UI ends
            var newMakeConnectionTask = pipe.WaitForConnectionAsync();
            var timeoutTask = Task.Delay(timeout);
            Task firstCompletedTask = Task.WhenAny(newMakeConnectionTask, ciProcessTask, timeoutTask).Result;
            if (firstCompletedTask == newMakeConnectionTask)
            {
                Console.WriteLine("Found listener");
                return pipe; //connection made
            }
            else if (firstCompletedTask == ciProcessTask)
            {
                Console.WriteLine("CI.UI stopped unexpectedly");
                return null;
            }
            else
            {
                Console.WriteLine("Dispatching the message to CI.UI timed out");
                return null;
            }
        }

        private static Task StartCIUI()
        {
            try
            {
                Console.WriteLine("Starting CI.UI");
#if DEBUG
                return Task.Run((Action)ReceivingPipe.Start).ContinueWith(UI.Program.OutputError, TaskContinuationOptions.OnlyOnFaulted);
#else
                string ci_exe_path = CI_UI_Path;
                var process = Process.Start(ci_exe_path);
                return process.WaitForExitAsync();
#endif
            }
            catch (Exception e)
            {
                UI.Program.OutputError(e);
                return null;
            }
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
