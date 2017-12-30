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
    public class Program : IDisposable
    {
        public static void Main(string[] args)
        {
            Logger.Log("In UI.main");

            if (args.Length != 0)
            {
#if DEBUG
                using (var program = new Program())
                {
                    Logger.Log($"Directly handling message {string.Join(" ", args)}");
                    program.HandleInput(args);
                    Logger.Log("Done directly handling");
                }
                Console.ReadLine();
#else
                string s = "Legacy direct call deprecated; call via CI.Dispatcher";
                Logger.Log(s);
                throw new ArgumentException(s);
#endif
            }
            else
            {
                Start();
            }
        }

        public static void Start(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var program = new Program())
                program.start(cancellationToken);
        }
        private void start(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var uiDispatcher = Dispatcher.CurrentDispatcher;
                cancellationToken.Register(() => uiDispatcher.InvokeShutdown());
                CIReceivingPipe.Start(this, cancellationToken);
                Dispatcher.Run(); // for icon etc
            }
            catch (Exception e)
            {
                Logger.Log("Exited message pump with error");
                OutputError(e);
            }
            finally
            {
                Logger.Log("Disposing UI");
            }
        }

        private readonly NotificationIcon icon = new NotificationIcon();
        public void OutputError(Task task)
        {
            Contract.Requires(task != null);
            Contract.Requires(task.IsFaulted);

            OutputError(task.Exception);
        }
        public void OutputError(Exception e)
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
            if (!TestClassExtensions.IsRunningFromUnitTest)
                Console.ReadLine();
#else
            icon.ShowErrorBalloon(e.Message, e is ArgumentException ? Status.ArgumentError : Status.UnhandledException);
#endif
        }
        internal void HandleInput(string[] input, CancellationToken cancellationToken = default(CancellationToken))
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

            var work = new CopyBuildTestSolutions(solutionFilePath, ConfigurationManager.AppSettings["destinationDirectory"], hash);

            HandleCommit(work, icon, hash, cancellationToken);
        }
        internal static void HandleCommit(ICopyBuildTestSolutions work, NotificationIcon icon, string hash = null, CancellationToken externalCancellationToken = default(CancellationToken))
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            icon.CancellationRequested += onOperationCanceled;
            externalCancellationToken.Register(cancellationTokenSource.Cancel);

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
                foreach ((Status status, string message) in work.CopySolutionAndExecuteTestsDelegate(cancellationTokenSource.Token, out resultsFile, out commitMessage, out int projectCount))
                {
                    if (cancellationTokenSource.IsCancellationRequested)
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
                cancellationTokenSource.Dispose();
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
                cancellationTokenSource.Cancel();
            }
        }

        void IDisposable.Dispose()
        {
            this.icon?.Dispose();
        }
    }
}