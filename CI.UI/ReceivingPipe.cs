using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.IO;
using System.Threading;
using JBSnorro.GitTools.CI;

namespace CI.UI
{
    public static class ReceivingPipe
    {
        public static readonly string PipeName = "CI_Messaging";
        public static readonly string Separator = "-,-";
        public static void Start()
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
                            Thread.Sleep(100);
                        }
                    }

                    Logger.Log($"Received message {processedMessageCount}");
                    string[] args = message.Split(new string[] { Separator }, StringSplitOptions.None);
                    Program.HandleInput(args);
                    Logger.Log($"Processed message {processedMessageCount}");
                    processedMessageCount++;
                }
            }
        }
    }
}
