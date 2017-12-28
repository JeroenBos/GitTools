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

            IEnumerable<(Status Status, string Message)> getLog(CancellationToken cancellationToken, out TestResultsFile resultsFile, out string commitMessage, out int projectCount)
            {
                return JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(solutionFilePath,
                                                                                ConfigurationManager.AppSettings["destinationDirectory"],
                                                                                out resultsFile,
                                                                                out commitMessage,
                                                                                out projectCount,
                                                                                hash,
                                                                                cancellationToken);
            }

            HandleCommit(icon, getLog, hash);
        }
        internal static void HandleCommit(IEnumerable<(Status, string)> log, NotificationIcon icon, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
        {
            HandleCommit(() => log, icon, resultsFile, commitMessage, projectCount, hash);
        }
        internal static void HandleCommit(Func<IEnumerable<(Status, string)>> getLog, NotificationIcon icon, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
        {
            var wrappedGetlog = new CopySolutionAndExecuteTestsDelegate(implementation);
            IEnumerable<(Status, string)> implementation(CancellationToken cancellationToken, out TestResultsFile resultsFile_out, out string commitMessage_out, out int projectCount_out)
            {
                resultsFile_out = resultsFile;
                commitMessage_out = commitMessage;
                projectCount_out = projectCount;
                return getLog();
            }
            HandleCommit(icon, wrappedGetlog, hash);
        }
        private static void HandleCommit(NotificationIcon icon, CopySolutionAndExecuteTestsDelegate getLog, string hash)
        {
            bool cancelRequested = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            icon.CancellationRequested += onOperationCanceled;

            Logger.Log("Processing message");
            DateTime start = DateTime.Now;
            icon.Text = "Starting...";
            icon.Percentage = 0.1;
            icon.Status = NotificationIconStatus.Working;
            TestResult overallStatus = TestResult.Failure;
            int builtProjectsCount = 0;
            int successfulTestsCount = 0;
            int failedTestCount = 0;
            string balloonMessage = "";
            TestResultsFile resultsFile = null;
            string commitMessage = "";
            try
            {
                foreach ((Status status, string message) in getLog(cancellationTokenSource.Token, out resultsFile, out commitMessage, out int projectCount))
                {
                    if (cancelRequested)
                    {
                        Logger.Log("Processing message canceled by user");
                        icon.Status = NotificationIconStatus.Default;
                        icon.Text = null;
                        return;
                    }

                    switch (status)
                    {
                        case Status.Skipped:
                            Logger.Log($"Skipped: The specified commit does not satisfy the conditions to be built and tested. {message}");
                            icon.Status = NotificationIconStatus.Default;
                            overallStatus = TestResult.Ignored;
                            break;

                        case Status.BuildSuccess:
                            builtProjectsCount++;
                            icon.Status = NotificationIconStatus.Working;
                            icon.Percentage = GetEstimatedPercentage();
                            icon.Text = $"{builtProjectsCount}/{projectCount} projects built";
                            Logger.Log(message);
                            break;

                        case Status.TestSuccess:
                            successfulTestsCount++;
                            if (icon.Status != NotificationIconStatus.Bad)
                            {
                                icon.Status = NotificationIconStatus.Working;
                                icon.Text = $"{successfulTestsCount}/{getTotalTestCount()} tests successful";
                            }
                            icon.Percentage = GetEstimatedPercentage();
                            break;

                        case Status.TestError:
                            Logger.Log($"{status.ToTitle()}: " + message);

                            failedTestCount++;
                            balloonMessage += message + "\n";

                            icon.Percentage = GetEstimatedPercentage();
                            icon.Text = $"{failedTestCount}/{getTotalTestCount()} tests failed";
                            icon.ShowErrorBalloon(balloonMessage, status);
                            break;

                        case Status.ArgumentError:
                        case Status.MiscellaneousError:
                        case Status.ProjectLoadingError:
                        case Status.BuildError:
                        case Status.UnhandledException:
                            Logger.Log($"{status.ToTitle()}: " + message);
                            icon.Percentage = 1;
                            icon.Text = null;
                            icon.ShowErrorBalloon(message, status);
                            return;

                        case Status.Success:
                            Logger.Log("OK: " + message);
                            icon.Percentage = 1;
                            icon.Text = $"Done. {successfulTestsCount} tests successful";
                            icon.Status = NotificationIconStatus.Ok;
                            overallStatus = TestResult.Success;
                            break;
                        default:
                            throw new DefaultSwitchCaseUnreachableException();
                    }
                }
            }
            finally
            {
                icon.Percentage = 1; // removes the cancel button
                icon.CancellationRequested -= onOperationCanceled;
                if (icon.Status == NotificationIconStatus.Working)
                    icon.Status = NotificationIconStatus.Ok;

                if (resultsFile != null)
                {
                    try
                    {
                        if (hash != null)
                        {
                            resultsFile.Append(hash, overallStatus, commitMessage, (int)Math.Ceiling((DateTime.Now - start).TotalSeconds), successfulTestsCount);
                        }
                    }
                    catch { }

                    resultsFile.Dispose();
                }
            }

            double GetEstimatedPercentage()
            {
                if (resultsFile == null || resultsFile.Estimate == TimingEstimator.UnknownEstimate)
                    return 0;

                float result = (float)(DateTime.Now - start).TotalSeconds / resultsFile.Estimate;
                if (result > 1)
                    return 0.99; // cannot be equal to 1, because that means done and things would get updated
                else
                    return result;
            }
            string getTotalTestCount()
            {
                if (resultsFile == null || successfulTestsCount >= resultsFile.TestCount)
                {
                    return "?";
                }
                else
                {
                    return resultsFile.TestCount.ToString();
                }
            }
            void onOperationCanceled(object sender, EventArgs e)
            {
                cancelRequested = true;
                cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// An abstraction such that HandleCommit can be tested more easily.
        /// </summary>
        internal delegate IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTestsDelegate(CancellationToken cancellationToken, out TestResultsFile resultsFile, out string commitMessage, out int projectCount);
    }
}