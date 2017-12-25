using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace CI.UI
{
    public class Program
    {
        private readonly static NotificationIcon icon = new NotificationIcon();

        public static void Main(string[] args)
        {
            Logger.Log("In UI.main");
#if !DEBUG
            if (args.Length != 0)
            {
                string s = "Legacy direct call deprecated; call via CI.Dispatcher";
                Logger.Log(s);
                throw new ArgumentException(s);
            }
#endif

            try
            {
#if DEBUG
                if (args.Length != 0)
                {
                    Logger.Log($"Directly handling message {string.Join(" ", args)}");
                    HandleInput(args);
                    Logger.Log("Done");
                    Console.ReadLine();
                    return;
                }
#endif
                Logger.Log("Starting message pump");
                Dispatcher.CurrentDispatcher.InvokeAsync(LoggedReceivingPipeStart);
                Dispatcher.Run(); //required for buttons on Notify Icon
            }
            finally
            {
                icon.Dispose();
                Logger.Log("Disposing UI");
            }

            async void LoggedReceivingPipeStart()
            {
                try
                {
                    await ReceivingPipe.Start();
                }
                catch (Exception e)
                {
                    Logger.Log("Exited message pump with error");
                    OutputError(e);
                }
            }
        }
        public static void OutputError(Task task)
        {
            Contract.Requires(task != null);
            Contract.Requires(task.IsFaulted);

            OutputError(task.Exception);
        }
        public static void OutputError(Exception e)
        {
            if (e is AggregateException agr)
            {
                OutputError(agr.InnerException);
                return;
            }
            Debug.WriteLine(e.StackTrace);
            Debug.WriteLine(e.Message);
            Logger.Log(e.Message);
            Logger.Log(e.StackTrace);
#if DEBUG
            icon.Status = NotificationIconStatus.Bad;
            Console.ReadLine();
#else
            icon.ShowErrorBalloon(e.Message, e is ArgumentException ? Status.ArgumentError : Status.UnhandledException);
#endif
        }
        internal static void HandleInput(string[] input)
        {
            if (input == null || input.Length == 0) throw new ArgumentException("No arguments were provided");

            if (input.Length > 2)
                throw new ArgumentException($"Too many arguments provided: expected 1 or 2, received {input.Length}");

            if (!input[0].EndsWith(".sln"))
                throw new ArgumentException("The first argument is expected to be a .sln file");
            if (!File.Exists(input[0]))
                throw new ArgumentException($"The file '{input[0]}' could not be found");
            string solutionFilePath = input[0];

            string hash = null;
            if (input.Length == 2)
            {
                if (!GitCommandLine.IsValidCommitHash(input[1]))
                    throw new ArgumentException($"The second argument, the commit hash {input[1]}, is not a valid hash");
                hash = input[1];
            }

            HandleCommit(solutionFilePath, hash);
        }
        private static void HandleCommit(string solutionFilePath, string hash)
        {
            Contract.Requires(!string.IsNullOrEmpty(solutionFilePath));

            icon.Status = NotificationIconStatus.Working(0);
            Logger.Log("Processing message");
            TestResultsFile resultsFile = null;
            try
            {
                var log = JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(solutionFilePath,
                                                                                   ConfigurationManager.AppSettings["destinationDirectory"],
                                                                                   out resultsFile,
                                                                                   hash);

                foreach ((Status status, string message) in log)
                {
                    switch (status)
                    {
                        case Status.Success:
                            Logger.Log("OK: " + message);
                            icon.Status = NotificationIconStatus.Ok;
                            break;

                        case Status.Skipped:
                            Logger.Log($"Skipped: The specified commit does not satisfy the conditions to be built and tested. {message}");
                            icon.Status = NotificationIconStatus.Default;
                            break;

                        case Status.TestSuccess:
                            // TODO: some form of progress bar
                            break;

                        case Status.BuildSuccess:
                            Logger.Log(message);
                            break;

                        case Status.ArgumentError:
                        case Status.MiscellaneousError:
                        case Status.ProjectLoadingError:
                        case Status.BuildError:
                        case Status.TestError:
                        case Status.UnhandledException:
                            Logger.Log($"{status.ToTitle()}: " + message);
                            icon.ShowErrorBalloon(message, status);
                            break;
                        default:
                            throw new DefaultSwitchCaseUnreachableException();
                    }
                }
            }
            finally
            {
                if (resultsFile != null)
                    resultsFile.Dispose();
            }
        }
    }
}