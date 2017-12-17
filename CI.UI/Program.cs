using CI.UI.Properties;
using JBSnorro.GitTools;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CI.UI
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                HandleInput(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
#if DEBUG
                Console.ReadLine();
#else
                using (var icon = new NotificationIcon())
                {
                    icon.ShowErrorBalloon(e.Message, e is ArgumentException ? Status.ArgumentError : Status.UnhandledException);
                    Thread.Sleep(NotificationIcon.ErrorBalloonShowDuration);
                }
#endif
            }
        }

        private static void HandleInput(string[] input)
        {
            if (input == null || input.Length == 0) throw new ArgumentException("No arguments were provided");

            const int expectedArgumentCount = 2;
            if (input.Length != expectedArgumentCount)
                throw new ArgumentException($"Too {(input.Length > expectedArgumentCount ? "many" : "few")} arguments provided: expected {expectedArgumentCount}, given {input.Length}");

            if (!input[0].EndsWith(".sln"))
                throw new ArgumentException("The second argument to 'commit' is expected to be a .sln file");
            if (!File.Exists(input[0]))
                throw new ArgumentException($"The file '{input[0]}' could not be found");

            if (input[1].Length != GitCommandLine.CommitHashLength)
                throw new ArgumentException($"The commit hash has length {input[1].Length} where {GitCommandLine.CommitHashLength} was expected");

            HandleCommit(input[0], input[1]);
        }
        private static void HandleCommit(string solutionFilePath, string hash)
        {
            using (var icon = new NotificationIcon() { Status = NotificationIconStatus.Working })
            {
                var (status, message) = JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(solutionFilePath, Resources.destinationPath, hash);

                if (status == Status.Success)
                {
                    icon.Status = NotificationIconStatus.Ok;
                    Thread.Sleep(5000);
                }
                else
                {
                    Debug.Write(message);
                    icon.ShowErrorBalloon(message, status);
                    Thread.Sleep(NotificationIcon.ErrorBalloonShowDuration);
                }
            }
        }
    }
}
