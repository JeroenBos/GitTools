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
        /// This event notifies whenever a messages is received. Testing purposes only.
        /// </summary>
        internal static event Action<object, string> OnReceivedMessage;
        /// <summary>
        /// This event notifies whenever a messages has been handled. Testing purposes only. Is not triggered when the message was canceled (even if it completed).
        /// </summary>
        internal static event Action<object, string> OnHandledMessage;

        private static void InvokeOnReceivedMessage(ReceivingPipe pipe, string message)
        {
            OnReceivedMessage?.Invoke(pipe, message);
        }
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

        public async Task Start(CancellationToken cancellationToken)
        {
            try
            {
                int receivedMessageCount = 0;
                Logger.Log("Starting message pump client pipe with name " + this.PipeName);
                while (!cancellationToken.IsCancellationRequested)
                {
                    using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                    using (StreamReader reader = new StreamReader(pipe))
                    {
                        while (!pipe.IsConnected)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            await pipe.ConnectAsync(cancellationToken);

                            while (pipe.IsConnected)
                            {
                                string message = reader.ReadLine();
                                if (message == null)
                                    break;
                                if (cancellationToken.IsCancellationRequested)
                                    return;

                                Logger.Log($"Received message {receivedMessageCount++}. Enqueuing");
                                InvokeOnReceivedMessage(this, message);

                                string[] messageParts = message.Split(new string[] { Separator }, StringSplitOptions.None);
                                HandleMessage(messageParts, cancellationToken);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Message pump experienced unexpected error: " + e.Message);
            }
            finally
            {
                Logger.Log("Stopped message pump client pipe");
            }
        }
        /// <summary>
        /// Handles a received message.
        /// </summary>
        /// <param name="message"> The message, already split by the specified separator. </param>
        protected virtual void HandleMessage(string[] message, CancellationToken cancellationToken)
        {
            Contract.Requires(message != null);

            var completeMessage = string.Join(" ", message);
            if (!cancellationToken.IsCancellationRequested)
                InvokeOnHandledMessage(this, completeMessage);
            Logger.Log($"Handled message '{completeMessage}'");
        }
    }
}
