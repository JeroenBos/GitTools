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
            if (args.Length == 0) throw new ArgumentException("No arguments were provided");

            switch (args[0])
            {
                case "commit":
                    {
                        const int expectedArgumentCount = 3;
                        if (args.Length != expectedArgumentCount)
                            throw new ArgumentException($"Too {(args.Length > expectedArgumentCount ? "many" : "few")} arguments provided: expected {expectedArgumentCount}, given {args.Length}");
                        if (!args[1].EndsWith(".sln"))
                            throw new ArgumentException("The second argument to 'commit' is expected to be a .sln file");
                        if (!File.Exists(args[1]))
                            throw new ArgumentException($"The file '{args[1]}' could not be found");
                        if (args[2].Length != GitCommandLine.CommitHashLength)
                            throw new ArgumentException($"The commit hash has length {args[2].Length} where {GitCommandLine.CommitHashLength} was expected");

                        HandleCommit(args[1], args[2]);
                    }
                    break;
                default:
                    throw new ArgumentException("The first argument was not any of the expected values");
            }
        }

        private static void HandleCommit(string solutionFilePath, string hash)
        {
            using (var icon = new NotificationIcon() { Status = NotificationIconStatus.Working })
            {
                var (status, message) = JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(solutionFilePath, Resources.destinationPath, hash);

                if (status == Status.Success)
                {
                    icon.Status = NotificationIconStatus.Ok;
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
