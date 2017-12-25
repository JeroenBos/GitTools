using System.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// Reads many <see cref="NamedPipeServerStream"/> pipes and combines it into one.
    /// </summary>
    public class NamedPipesServerStream : IEnumerable<string>, IDisposable
    {
        private readonly List<NamedPipeServerStream> pipes = new List<NamedPipeServerStream>();
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        protected CancellationTokenSource CancellationTokenSource { get; }
        private readonly Func<string, bool> isQuitSignal;
        protected string PipeName { get; }
        private int expectedNumberOfConnections;
        private volatile int aliveConnections;
        private readonly bool spawnLazily;

        public NamedPipesServerStream(string pipeName, Func<string, bool> isQuitSignal, int expectedNumberOfConnections, bool spawnLazily = false)
        {
            this.PipeName = pipeName;
            this.isQuitSignal = isQuitSignal;
            this.expectedNumberOfConnections = expectedNumberOfConnections;
            this.spawnLazily = spawnLazily;
            this.CancellationTokenSource = new CancellationTokenSource();

            this.Spawn();
        }

        protected virtual NamedPipeServerStream CreateReader()
        {
            return new NamedPipeServerStream(this.PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances);
        }

        private void Spawn()
        {
            if (expectedNumberOfConnections == 0)
                return;

            Interlocked.Decrement(ref this.expectedNumberOfConnections);
            Interlocked.Increment(ref this.aliveConnections);
            var pipe = this.CreateReader();
            Task.Run(async () =>
            {
                try
                {
                    pipe.WaitForConnection();
                    if (spawnLazily)
                        Spawn();
                    await Loop(pipe);
                    Interlocked.Decrement(ref this.aliveConnections);
                }
                catch (TaskCanceledException)
                {
                }
            }, CancellationTokenSource.Token);
            if (!spawnLazily)
            {
                Spawn();
            }
        }
        private async Task Loop(NamedPipeServerStream pipe)
        {
            using (StreamReader reader = new StreamReader(pipe))
            {
                string message;
                do
                {
                    message = await reader.ReadLineAsync();
                    this.queue.Enqueue(message);
                } while (!this.isQuitSignal(message) && !CancellationTokenSource.IsCancellationRequested);
            }
        }

        public void Dispose()
        {
            CancellationTokenSource.Cancel();
            foreach (var pipe in this.pipes)
                pipe.Dispose();
        }

        public IEnumerator<string> GetEnumerator()
        {
            while ((this.aliveConnections != 0 || expectedNumberOfConnections != 0) && !this.CancellationTokenSource.IsCancellationRequested)
            {
                if (queue.TryDequeue(out string result))
                {
                    yield return result;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
