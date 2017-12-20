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
    class Dispatcher
    {
        private static string CI_UI_Path => ConfigurationManager.AppSettings["CI_UI_Path"];
        private const int timeout = 1000;

        /// <summary>
        /// The purpose of this application is for each time it is executed, dispatch the message to the only running instance of CI.UI.
        /// Once the message has been sent to CI.UI, execution stops.
        /// </summary>
        static void Main(string[] args)
        {
            NamedPipeServerStream pipe = null;
            Logger.Log("in dispatcher. args: " + string.Join(" ", args.Select(arg => '"' + arg + '"')));
            try
            {
                var message = ComposeMessage(args);
                if (message != null)
                {
                    pipe = SetupConnection();
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
                if (pipe != null)
                    pipe.Dispose();
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
            if (Process.GetProcessesByName("CI.UI").Length == 0)
            {
                Logger.Log("The receiving end of the pipe is not running");
                return null;
            }

            var pipe = new NamedPipeServerStream(ReceivingPipe.PipeName, PipeDirection.Out);
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
