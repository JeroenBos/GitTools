using System.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Collections;
using System.Collections.Concurrent;

namespace JBSnorro.GitTools.CI
{
    sealed class PipesReader : IEnumerable<string>, IDisposable
    {
        private readonly List<NamedPipeServerStream> pipes = new List<NamedPipeServerStream>();
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Func<NamedPipeServerStream> createReader;
        private readonly Func<string, bool> isQuitSignal;
        private int expectedNumberOfConnections;
        private volatile int aliveConnections;
        private readonly bool spawnLazily;

        public PipesReader(Func<NamedPipeServerStream> createReader, Func<string, bool> isQuitSignal, int expectedNumberOfConnections, bool spawnLazily = false)
        {
            this.createReader = createReader;
            this.isQuitSignal = isQuitSignal;
            this.expectedNumberOfConnections = expectedNumberOfConnections;
            this.spawnLazily = spawnLazily;

            this.Spawn();
        }

        private void Spawn()
        {
            if (expectedNumberOfConnections == 0)
                return;

            Interlocked.Decrement(ref this.expectedNumberOfConnections);
            Interlocked.Increment(ref this.aliveConnections);
            var pipe = createReader();
            new Thread(() =>
            {
                pipe.WaitForConnection();
                if (spawnLazily)
                    Spawn();
                Loop(pipe);
                Interlocked.Decrement(ref this.aliveConnections);
            })
            .Start();
            if (!spawnLazily)
            {
                Spawn();
            }
        }
        private void Loop(NamedPipeServerStream pipe)
        {
            using (StreamReader reader = new StreamReader(pipe))
            {
                string message;
                do
                {
                    message = reader.ReadLine();
                    this.queue.Enqueue(message);
                } while (!this.isQuitSignal(message));
            }
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            foreach (var pipe in this.pipes)
                pipe.Dispose();
        }

        public IEnumerator<string> GetEnumerator()
        {
            while ((this.aliveConnections != 0 || expectedNumberOfConnections != 0) && !this.cancellationTokenSource.IsCancellationRequested)
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
