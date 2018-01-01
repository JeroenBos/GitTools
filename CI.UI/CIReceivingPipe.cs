using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CI.UI
{
    public sealed class CIReceivingPipe : ReceivingPipe, IDisposable
    {
        public static string GetPipeName() => ConfigurationManager.AppSettings["CI_PIPE_NAME"] ?? "CI_PIPE";
        public const string PipeMessageSeparator = "-:-";

        /// <summary>
        /// Gets the dispatcher that created this pipe. 
        /// </summary>
        public Dispatcher MainDispatcher { get; }
        /// <summary>
        /// Gets the program that is currently executing.
        /// </summary>
        public Program Program { get; }
        /// <summary>
        /// Gets the cancellation token for this pipe.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        private readonly ConcurrentQueue<(string[], CancellationToken)> messageQueue;
        private readonly ManualResetEvent MessageEvent;
        private readonly object messageEventLock = new object();
        private readonly Thread messageHandlingThread;
        private bool isDisposed;

        public CIReceivingPipe(Program program)
            : base(GetPipeName(), PipeMessageSeparator)
        {
            Contract.Requires(program != null);

            this.Program = program;
            this.MainDispatcher = Dispatcher.CurrentDispatcher;
            this.MessageEvent = new ManualResetEvent(false);
            this.messageQueue = new ConcurrentQueue<(string[], CancellationToken)>();

            this.messageHandlingThread = new Thread(processQueue) { IsBackground = true };
            this.messageHandlingThread.Start();
        }

        protected override void HandleMessage(string[] message, CancellationToken cancellationToken)
        {
            messageQueue.Enqueue((message, cancellationToken));
            lock (messageEventLock)
            {
                MessageEvent.Set();
            }
        }
        private void processQueue()
        {
            while (!isDisposed)
            {
                if (messageQueue.TryDequeue(out (string[], CancellationToken) tuple))
                {
                    handleMessageImplementation(tuple.Item1, tuple.Item2);
                }
                else
                {
                    lock (messageEventLock)
                    {
                        MessageEvent.Reset();
                    }
                    MessageEvent.WaitOne();
                }
            }
        }
        private void handleMessageImplementation(string[] message, CancellationToken cancellationToken)
        {
            Logger.Log($"Handling message '{string.Join(" '", message)}'");
            Exception debug = null;
            try
            {
                Program.HandleInput(message, cancellationToken);
            }
            catch (Exception e) when (e is TaskCanceledException || (e is AggregateException ae && ae.InnerException is TaskCanceledException))
            {
                debug = e;
                cancellationToken = new CancellationToken(true); // affects how the base handler works
            }
            catch (Exception e)
            {
                debug = e;
                MainDispatcher.InvokeAsync(() => Program.OutputError(debug));
            }
            finally
            {
                base.HandleMessage(message, cancellationToken);
            }
        }

        public void Dispose()
        {
            Logger.Log("Disposing receiving pipe");
            //gracefully cancel thread before disposing of ManualResetEvent
            this.isDisposed = true;
            this.MessageEvent.Set();
            this.messageHandlingThread.Join();

            this.MessageEvent.Dispose();
            Logger.Log("Disposed receiving pipe");
        }
    }
}
