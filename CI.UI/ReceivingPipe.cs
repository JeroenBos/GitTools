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

namespace JBSnorro
{
    public class ReceivingPipe
    {
        /// <summary>
        /// Starts a new receiving pipe.
        /// </summary>
        /// <param name="pipeName"> The name of the pipe to receive on. </param>
        /// <param name="separator"> The separator character sequence between messages on this pipe. </param>
        /// <param name="ownDispatcher"> True indicates the pipe reader is executed on a new dispatcher; otherwise the calling dispatcher is used. </param>
        public static async Task<ReceivingPipe> Start(string pipeName, string separator, bool ownDispatcher = false)
        {
            ReceivingPipe ctor(string arg0, string arg1, Dispatcher arg2) => new ReceivingPipe(arg0, arg1, arg2);
            return await Start<ReceivingPipe>(pipeName, separator, ownDispatcher, ctor);
        }
        protected static async Task<TReceivingPipe> Start<TReceivingPipe>(string pipeName, string separator, bool ownDispatcher, Func<string, string, Dispatcher, TReceivingPipe> ctor) where TReceivingPipe : ReceivingPipe
        {
            Contract.Requires(ctor != null);

            var executingDispatcher = ownDispatcher ? StartDispatcher(timeout: TimeSpan.FromMilliseconds(100)) : Dispatcher.CurrentDispatcher;
            var result = ctor(pipeName, separator, executingDispatcher);
            await executingDispatcher.InvokeAsync(result.Loop);
            return result;
        }
        private static Dispatcher StartDispatcher(TimeSpan timeout = default(TimeSpan))
        {
            Dispatcher executingDispatcher = null;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                executingDispatcher = Dispatcher.CurrentDispatcher;
                Dispatcher.Run();
            });

            DateTime start = DateTime.Now;

            while (executingDispatcher == null)
            {
                Thread.Sleep(1);

                bool isTimedOut = timeout != default(TimeSpan) && DateTime.Now - start > timeout;
                if (isTimedOut)
                    break;
            }

            if (executingDispatcher == null)
                throw new TimeoutException("Running a new dispatcher failed");
            return executingDispatcher;
        }
        /// <summary>
        /// This event notifies whenever a messages is received. Testing purposes only.
        /// </summary>
        internal static event Action<object, string> OnReceiveMessage;

        /// <summary>
        /// Gets the name of this pipe.
        /// </summary>
        public string PipeName { get; }
        /// <summary>
        /// Gets the separator between messages on this pipe.
        /// </summary>
        public string Separator { get; }

        /// <summary>
        /// Gets the dispatcher running the receiver loop.
        /// </summary>
        public Dispatcher ExecutingDispatcher { get; }

        /// <summary>
        /// Creates a new receiving pipe.
        /// </summary>
        /// <param name="pipeName"> The name of the pipe to receive on. </param>
        /// <param name="separator"> The separator character sequence between messages on this pipe. </param>
        /// <param name="executingDispatcher"> The dispatcher running the receiver loop. </param>
        protected ReceivingPipe(string pipeName, string separator, Dispatcher executingDispatcher)
        {
            Contract.Requires(!string.IsNullOrEmpty(pipeName));
            Contract.Requires(!string.IsNullOrEmpty(separator));
            Contract.Requires(separator.Length != 0);
            Contract.Requires(executingDispatcher != null);

            this.PipeName = pipeName;
            this.Separator = separator;
            this.ExecutingDispatcher = executingDispatcher;
        }

        protected async Task Loop()
        {
            Contract.Requires<InvalidOperationException>(this.ExecutingDispatcher == Dispatcher.CurrentDispatcher, "Called from wrong dispatcher");

            int processedMessageCount = 0;
            Logger.Log("Starting client pipe");
            while (true)
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                using (StreamReader reader = new StreamReader(pipe))
                {
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
                            await Task.Delay(10);
                        }
                    }
                    Logger.Log($"Received message {processedMessageCount}");

                    var warningCS4014SuppressingVariable = this.ExecutingDispatcher.InvokeAsync(() =>
                        {
                            string[] messageParts = message.Split(new string[] { Separator }, StringSplitOptions.None);
                            HandleMessage(messageParts);
                        });
                    processedMessageCount++;
                }
            }
        }
        /// <summary>
        /// Handles a received message.
        /// </summary>
        /// <param name="message"> The message, already split by the specified separator. </param>
        protected virtual void HandleMessage(string[] message)
        {
            Contract.Requires(message != null);
        }
    }
}
