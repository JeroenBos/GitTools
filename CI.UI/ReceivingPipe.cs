using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.IO;
using System.Threading;

namespace CI.UI
{
    public static class ReceivingPipe
    {
        public static readonly string PipeName = "CI_Messaging";
        public static readonly string Separator = "-,-";
        public static void Start()
        {
            int processedMessageCount = 0;
            Console.WriteLine("Starting client pipe");
            while (true)
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                {
                    StreamReader reader = new StreamReader(pipe);

                    while (!pipe.IsConnected)
                    {
                        try
                        {
                            pipe.Connect(0);
                        }
                        catch (TimeoutException)
                        {
                            Thread.Sleep(100);
                        }
                    }

                    string message = reader.ReadLine();
                    if (message != null)
                    {
                        string[] args = message.Split(new string[] { Separator }, StringSplitOptions.None);
                        Program.HandleInput(args);
                        Console.WriteLine($"Processed message {processedMessageCount}");
                        processedMessageCount++;
                    }
                }
            }
        }
    }
}
