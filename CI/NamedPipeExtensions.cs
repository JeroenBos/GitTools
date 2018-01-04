using JBSnorro.Diagnostics;
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace JBSnorro
{
    public static class NamedPipeExtensions
    {
        /// <summary>
        /// Asynchronously connects to a waiting server within the specified timeout period and monitors cancellation requests.
        /// Does so more CPU economically than <see cref="NamedPipeClientStream.ConnectAsync(int, CancellationToken)"/>.
        /// </summary>
        /// <exception cref="TaskCanceledException">The task has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The provided cancellationToken has already been disposed.</exception>
        public static async Task ConnectAsyncWithBetterPerformance(this NamedPipeClientStream pipe,
                                                                   string pipeName,
                                                                   CancellationToken cancellationToken,
                                                                   int retryDelay_ms = 10)
        {
            Contract.Requires(pipe != null);
            Contract.Requires(!string.IsNullOrEmpty(pipeName));
            Contract.Requires(retryDelay_ms >= 0, nameof(retryDelay_ms));

            while (true)
            {
                if (NamedPipeExists(pipeName))
                {
                    pipe.Connect(1);
                    if (pipe.IsConnected)
                        return;
                }
                else
                {
                    await Task.Delay(retryDelay_ms, cancellationToken);
                }
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool WaitNamedPipe(string name, int timeout);

        /// <remakrs>https://social.msdn.microsoft.com/Forums/en-US/7bbf5a0b-3c22-4836-b271-999e514c321b/namedpipeclientstreamconnect-causes-cpu-hang-is-there-a-workaround?forum=netfxnetcom&prof=required</remakrs>
        private static bool NamedPipeExists(string pipeName)
        {
            const int PIPE_DOES_NOT_EXIST = 0;
            const int FILE_NOT_FOUND = 2;
            try
            {
                int timeout = 0;
                string normalizedPath = Path.GetFullPath(string.Format(@"\\.\pipe\{0}", pipeName));
                bool exists = WaitNamedPipe(normalizedPath, timeout);
                if (!exists)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == PIPE_DOES_NOT_EXIST || error == FILE_NOT_FOUND)
                        return false;
                    // all other errors indicate other issues
                }
                return true;
            }
            catch
            {
                return false; // assume it doesn't exist
            }
        }

        /// <summary>
        /// Asynchronously waits for a client to connect to this <see cref="NamedPipeServerStream"/> object and monitors cancellation requests.
        /// Actually monitors cancellation requests, as opposed to the .NET implementation.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous wait operation.</returns>
        public static async Task WaitForConnectionAsyncWithCancellation(this NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            Contract.Requires(pipe != null);

            if (pipe.IsConnected)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            bool connected = false;

            IAsyncResult asyncResult = null;
            try
            {
                asyncResult = pipe.BeginWaitForConnection(onConnect, null);

                void onConnect(object _)
                {
                    connected = true;
                }

                await Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (connected)
                            return;

                        await Task.Delay(10, cancellationToken);
                    }
                });
            }
            finally
            {
                if (asyncResult != null)
                    pipe.EndWaitForConnection(asyncResult);
            }
        }
    }
}
