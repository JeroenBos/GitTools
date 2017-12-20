using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using JBSnorro.GitTools.CI;
using System.Windows.Threading;
using JBSnorro.Diagnostics;

namespace CI.UI
{
    public static class ReceivingPipe
    {
        static ReceivingPipe()
        {
            mainDispatcher = Dispatcher.CurrentDispatcher;
            SetupBackground();
        }
        private static readonly Dispatcher mainDispatcher;
        private static Dispatcher executingDispatcher;
        private static void SetupBackground()
        {
            Contract.Requires(executingDispatcher == null);

            ThreadPool.QueueUserWorkItem((object mainThread) =>
            {
                if (Thread.CurrentThread == mainThread) throw new Exception("The background thread is the foreground thread");

                executingDispatcher = Dispatcher.CurrentDispatcher;
                Dispatcher.Run();
            }, Thread.CurrentThread);
        }


        public static readonly string PipeName = "CI_Messaging";
        public static readonly string Separator = "-,-";
        public static async Task Start()
        {
            int processedMessageCount = 0;
            Logger.Log("Starting client pipe");
            while (true)
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                {
                    StreamReader reader = new StreamReader(pipe);

                    string message = null;
                    while (!pipe.IsConnected && message == null)
                    {
                        try
                        {
                            pipe.Connect(0);
                            message = reader.ReadLine();
                        }
                        catch (TimeoutException)
                        {
                            await Dispatcher.Yield();
                        }
                    }

                    Logger.Log($"Received message {processedMessageCount}");
                    await executingDispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                string[] args = message.Split(new string[] { Separator }, StringSplitOptions.None);
                                Program.HandleInput(args);
                            }
                            catch(Exception e)
                            {
                                mainDispatcher.InvokeAsync(() => Program.OutputError(e));
                            }
                        });
                    processedMessageCount++;
                }
            }
        }
    }
}
