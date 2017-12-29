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
using System.Diagnostics;

namespace JBSnorro
{
    public class ReceivingPipe
    {
        /// <summary>
        /// Starts a new receiving pipe.
        /// </summary>
        /// <param name="pipeName"> The name of the pipe to receive on. </param>
        /// <param name="separator"> The separator character sequence between messages on this pipe. </param>
        /// <param name="cancellationToken"> A token with which reading from the pipe can be canceled. </param>
        public static ReceivingPipe Start(string pipeName, string separator, CancellationToken cancellationToken = default(CancellationToken))
        {
            ReceivingPipe ctor(string arg0, string arg1) => new ReceivingPipe(arg0, arg1);
            return Start<ReceivingPipe>(pipeName, separator, ctor, cancellationToken);
        }
        protected static TReceivingPipe Start<TReceivingPipe>(string pipeName, string separator, Func<string, string, TReceivingPipe> ctor, CancellationToken cancellationToken = default(CancellationToken)) where TReceivingPipe : ReceivingPipe
        {
            Contract.Requires(ctor != null);

            var result = ctor(pipeName, separator);
            ThreadPool.QueueUserWorkItem(async _ => await result.loop(cancellationToken));
            return result;
        }
        /// <summary>
        /// This event notifies whenever a messages is received. Testing purposes only.
        /// </summary>
        internal static event Action<object, string> OnReceivedMessage;
        /// <summary>
        /// This event notifies whenever a messages has been handled. Testing purposes only.
        /// </summary>
        internal static event Action<object, string> OnHandledMessage;

        [Conditional("DEBUG")]
        private static void InvokeOnReceivedMessage(ReceivingPipe pipe, string message)
        {
            OnReceivedMessage?.Invoke(pipe, message);
        }
        [Conditional("DEBUG")]
        private static void InvokeOnHandledMessage(ReceivingPipe pipe, string message)
        {
            OnHandledMessage?.Invoke(pipe, message);
        }

        /// <summary>
        /// Gets the name of this pipe.
        /// </summary>
        public string PipeName { get; }
        /// <summary>
        /// Gets the separator between messages on this pipe.
        /// </summary>
        public string Separator { get; }

        /// <summary>
        /// Creates a new receiving pipe.
        /// </summary>
        /// <param name="pipeName"> The name of the pipe to receive on. </param>
        /// <param name="separator"> The separator character sequence between messages on this pipe. </param>
        protected ReceivingPipe(string pipeName, string separator)
        {
            Contract.Requires(!string.IsNullOrEmpty(pipeName));
            Contract.Requires(!string.IsNullOrEmpty(separator));
            Contract.Requires(separator.Length != 0);

            this.PipeName = pipeName;
            this.Separator = separator;
        }

        private async Task loop(CancellationToken cancellationToken)
        {
            int processedMessageCount = 0;
            Logger.Log("Starting message pump client pipe");
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                using (StreamReader reader = new StreamReader(pipe))
                {
                    string message = null;
                    while (!pipe.IsConnected && message == null)
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;
                            pipe.Connect(0);

                            if (cancellationToken.IsCancellationRequested)
                                break;
                            message = reader.ReadLine();
                        }
                        catch (TimeoutException)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;
                            await Task.Delay(10);
                            continue;
                        }

                        Logger.Log($"Received message {processedMessageCount}");
                        InvokeOnReceivedMessage(this, message);

                        Logger.Log($"Handling message '{message}'");
                        string[] messageParts = message.Split(new string[] { Separator }, StringSplitOptions.None);
                        HandleMessage(messageParts);
                        InvokeOnHandledMessage(this, message);
                        Logger.Log($"Handled message '{message}'");
                        processedMessageCount++;
                    }
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
