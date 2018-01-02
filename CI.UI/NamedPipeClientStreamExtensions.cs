using JBSnorro.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CI.UI
{
    public static class NamedPipeClientStreamExtensions
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
    }
}
