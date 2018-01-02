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
        private int readMessagesCount;
        private readonly object enumeratorDispensedLock = new object();
        private bool enumeratorDispensed;
        public int ReadMessagesCount => readMessagesCount;

        /// <remarks> This function ensures that the stream is disposed of, created lazily and that the enumerator can only be obtained once. </remarks>
        public static IEnumerable<TResult> Read<TResult>(Func<IEnumerable<string>, IEnumerable<TResult>> parse,
                                                         string pipeName,
                                                         Func<string, bool> isQuitSignal,
                                                         int expectedNumberOfConnections,
                                                         CancellationToken cancellationToken = default(CancellationToken),
                                                         bool spawnLazily = false)
        {
            using (var stream = new NamedPipesServerStream(pipeName, isQuitSignal, expectedNumberOfConnections, cancellationToken, spawnLazily))
            {
                foreach (TResult result in parse(stream))
                {
                    yield return result;
                }
            }
        }
        private NamedPipesServerStream(string pipeName, Func<string, bool> isQuitSignal, int expectedNumberOfConnections, CancellationToken cancellationToken = default(CancellationToken), bool spawnLazily = false)
        {
            this.PipeName = pipeName;
            this.isQuitSignal = isQuitSignal;
            this.expectedNumberOfConnections = expectedNumberOfConnections;
            this.spawnLazily = spawnLazily;
            this.CancellationTokenSource = new CancellationTokenSource();

            cancellationToken.Register(this.Dispose);

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

            if (this.CancellationTokenSource.IsCancellationRequested)
                throw new TaskCanceledException();

            Interlocked.Decrement(ref this.expectedNumberOfConnections);
            Interlocked.Increment(ref this.aliveConnections);
            var pipe = this.CreateReader();
            this.pipes.Add(pipe);
            Task.Run(async () =>
            {
                try
                {
                    await pipe.WaitForConnectionAsync(this.CancellationTokenSource.Token);
                    if (spawnLazily)
                        Spawn();
                    await Loop(pipe);
                }
                catch (TaskCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
                finally
                {
                    Interlocked.Decrement(ref this.aliveConnections);
                    pipe.Dispose();
                }
            }, this.CancellationTokenSource.Token);

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
                    Interlocked.Increment(ref readMessagesCount);
                    this.queue.Enqueue(message);
                } while (!this.isQuitSignal(message) && !this.CancellationTokenSource.IsCancellationRequested);
            }
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing reader");
            this.CancellationTokenSource.Cancel();
            foreach (var pipe in this.pipes)
                pipe.Dispose();
        }

        public IEnumerator<string> GetEnumerator()
        {
            lock (enumeratorDispensedLock)
            {
                if (enumeratorDispensed)
                    throw new InvalidOperationException("This class cannot be enumerated multiple times");

                enumeratorDispensed = true;
            }

            while (!this.CancellationTokenSource.IsCancellationRequested)
            {
                if (queue.TryDequeue(out string result))
                {
                    yield return result;
                }
                else if (this.aliveConnections == 0 && expectedNumberOfConnections == 0)
                {
                    yield break;
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
