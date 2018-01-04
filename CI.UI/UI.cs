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
        internal const string TEST_ARGUMENT = "TEST_ARGUMENT";
        public const string DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT = "-disregard_parent_outcome";
        public static void Main(string[] args)
        {
            if (args.Length != 0)
            {
#if DEBUG
                using (var icon = new NotificationIcon(isVisible: true))
                using (var program = new Program(icon))
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

        /// <summary>
        /// Reads the pipe and handles the message pump for the icon.
        /// </summary>
        public static void Start(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var icon = new NotificationIcon(isVisible: true))
                Start(icon, cancellationToken);
        }
        /// <summary>
        /// Reads the pipe and handles the message pump for the specified icon.
        /// </summary>
        public static void Start(NotificationIcon icon, CancellationToken cancellationToken = default(CancellationToken))
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            using (var program = new Program(icon))
            {
                program.start(cancellationToken);
            }
        }
        private void start(CancellationToken cancellationToken = default(CancellationToken))
        {
            Logger.Log("In UI.start");

            CIReceivingPipe pipe = null;
            Contract.Requires(!this.dispatcher.HasShutdownFinished, "The dispatcher has already shut down");

            try
            {
                cancellationToken.Register(() => { Logger.Log("Shutting down uiDispatcher"); this.dispatcher.InvokeAsync(() => Dispatcher.ExitAllFrames()); });
                Dispatcher.CurrentDispatcher.Invoke(() => pipe = new CIReceivingPipe(this));
                Dispatcher.CurrentDispatcher.InvokeAsync(() => pipe.Start(cancellationToken));
                Dispatcher.Run(); // for icon and pipe reading
            }
            catch (Exception e)
            {
                Logger.Log("Exited message pump with error");
                OutputError(e);
            }
            finally
            {
                if (pipe != null)
                    pipe.Dispose();
                Logger.Log("Disposing UI");
            }
        }

        public static Dispatcher Dispatcher { get; private set; }
        private readonly Dispatcher dispatcher;
        private readonly NotificationIcon icon;



        private Program(NotificationIcon icon)
        {
            Contract.Requires(icon != null);

            this.icon = icon;
            this.dispatcher = Dispatcher.CurrentDispatcher;
        }
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
            {
                Logger.Log("Reading line (so not from unit test)");
                Console.ReadLine();
            }
#else
            icon.ShowErrorBalloon(e.Message, e is ArgumentException ? Status.ArgumentError : Status.UnhandledException);
#endif
        }
        internal void HandleInput(string[] input, CancellationToken externalCancellationToken = default(CancellationToken), bool ignoreParentFailed = false)
        {
            if (input == null || input.Length == 0) throw new ArgumentException("No arguments were provided");

            if (input[0] == TEST_ARGUMENT)
            {
                HandleTestInput(input, externalCancellationToken);
                return;
            }



            if (input.Length > 3)
                throw new ArgumentException($"Too many arguments provided: expected 1, 2 or 3, received {input.Length}");

            if (!input[0].EndsWith(".sln"))
                throw new ArgumentException("The first argument is expected to be a .sln file");
            if (!File.Exists(input[0]))
                throw new ArgumentException($"The file '{input[0]}' could not be found");
            string solutionFilePath = input[0];

            string hash = null;
            if (input.Length == 3)
            {
                if (input[2] != DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT)
                    throw new ArgumentException($"The only viable third argument is {DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT}");
                else
                    ignoreParentFailed = true;
            }
            if (input.Length >= 2)
            {
                if (!GitCommandLine.IsValidCommitHash(input[1]) && input[1] != DISREGARD_PARENT_COMMIT_OUTCOME_ARGUMENT)
                    throw new ArgumentException($"The second argument, the commit hash {input[1]}, is not a valid hash");
                hash = input[1];
            }

            var work = new CopyBuildTestSolutionInjection(solutionFilePath, ConfigurationManager.AppSettings["destinationDirectory"], hash);

            HandleCommit(work, icon, externalCancellationToken, ignoreParentFailed);
        }
        private void HandleTestInput(string[] input, CancellationToken externalCancellationToken)
        {
            Contract.Requires(input != null && input.Length != 0 && input[0] == TEST_ARGUMENT);

            if (input.Length == 1)
                throw new ArgumentException("No number of milliseconds of test work was provided. ");
            if (!int.TryParse(input[1], out int timeout))
                throw new ArgumentException("Invalid number of milliseconds of test work provided. ");

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                bool canceled = true;
                try
                {
                    icon.CancellationRequested += onOperationCanceled;
                    externalCancellationToken.Register(icon.RequestCancellation);

                    Task.Delay(timeout, cancellationTokenSource.Token).Wait();
                    canceled = false;
                }
                finally
                {
                    icon.CancellationRequested -= onOperationCanceled;
                    Logger.Log($"Finished test input: {(canceled ? "canceled" : "time elapsed")}");
                }

                void onOperationCanceled(object sender, EventArgs e)
                {
                    cancellationTokenSource.Cancel();
                }
            }

        }
        internal static void HandleCommit(ICopyBuildTestSolutions work,
                                          NotificationIcon icon,
                                          CancellationToken externalCancellationToken = default(CancellationToken),
                                          bool ignoreParentFailed = false)
        {
            Contract.Requires(work != null);

            Logger.Log("Processing message");
            DateTime start = DateTime.Now;

            using (Prework prework = work.Prework(ignoreParentFailed))
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                icon.CancellationRequested += onOperationCanceled;
                externalCancellationToken.Register(icon.RequestCancellation);
                string commitMessage = prework.CommitMessage;
                TestResult overallStatus;
                TestResultsFile resultsFile = prework.TestResultsFile;

                switch (prework.Status)
                {
                    case Status.Success:
                        overallStatus = TestResult.Success;
                        break;

                    case Status.Skipped:
                        Logger.Log($"Skipped: The specified commit {(commitMessage == null ? "" : $"({commitMessage})")} does not satisfy the conditions to be built and tested. {prework.Message}");
                        icon.Status = NotificationIconStatus.Default;
                        if (string.IsNullOrEmpty(icon.Text))
                            icon.Text = "This commit was skipped";
                        overallStatus = TestResult.Ignored;
                        break;

                    case Status.ParentFailed:
                        Contract.Assert(!ignoreParentFailed);
                        Logger.Log($"Skipped: The specified commit {(commitMessage == null ? "" : $"({commitMessage})")} does not satisfy the conditions to be built and tested. {prework.Message}");
                        ParentFailedTracker.Add(work);
                        if (icon.Status != NotificationIconStatus.BadParent)
                        {
                            icon.ShowErrorBalloon("Parent failed already", Status.ParentFailed);
                        }
                        else
                        {
                            icon.Status = NotificationIconStatus.BadParent;
                            icon.Text = prework.Message;
                        }
                        overallStatus = TestResult.Failure;
                        break;

                    case Status.Canceled:
                        overallStatus = TestResult.Success;
                        break;

                    case Status.ArgumentError:
                    case Status.MiscellaneousError:
                    case Status.UnhandledException:
                        string text = $"{prework.Status.ToTitle()}{(commitMessage == null ? "" : $" for commit '{commitMessage}'")}: {prework.Message}";
                        Logger.Log(text);
                        icon.Percentage = 1;
                        icon.Text = text;
                        icon.ShowErrorBalloon(prework.Message, prework.Status);
                        overallStatus = TestResult.Failure;
                        break;

                    case Status.CopyingError:
                    case Status.ProjectLoadingError:
                    case Status.BuildError:
                    case Status.TestError:
                    case Status.ProjectLoadSuccess:
                    case Status.BuildSuccess:
                    case Status.TestStarted:
                    case Status.TestSuccess:
                        throw new ContractException("Prework shouldn't return this status");
                    default:
                        throw new DefaultSwitchCaseUnreachableException();
                }

                int loadedProjectsCount = 0;
                int builtProjectsCount = 0;
                int successfulTestsCount = 0;
                int failedTestCount = 0;
                string balloonMessage = "";

                try
                {
                    if (overallStatus != TestResult.Success)
                        return;

                    icon.Text = "Starting...";
                    icon.Percentage = 0.1;
                    icon.Status = NotificationIconStatus.Working;


                    foreach ((Status status, string message) in work.CopySolutionAndExecuteTests(cancellationTokenSource.Token, out int projectCount))
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
                            case Status.ProjectLoadSuccess:
                                loadedProjectsCount++;
                                icon.Status = NotificationIconStatus.Working;
                                icon.Percentage = GetEstimatedPercentage();
                                icon.Text = $"{loadedProjectsCount}/{projectCount} projects loaded";
                                Logger.Log(message);
                                break;

                            case Status.BuildSuccess:
                                builtProjectsCount++;
                                icon.Status = NotificationIconStatus.Working;
                                icon.Percentage = GetEstimatedPercentage();
                                icon.Text = $"{builtProjectsCount}/{projectCount} projects built";
                                Logger.Log(message);
                                break;

                            case Status.TestStarted:
                                Logger.Log($"Testing " + message);
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
                                commitMessage = commitMessage == null ? "" : $" for commit '{commitMessage}'";
                                Logger.Log($"{status.ToTitle()}{commitMessage}: {message}");
                                overallStatus = TestResult.Failure;

                                failedTestCount++;
                                balloonMessage += message + "\n";

                                icon.Percentage = GetEstimatedPercentage();
                                icon.Text = $"{failedTestCount}/{getTotalTestCount()} tests failed{commitMessage}";
                                icon.ShowErrorBalloon(balloonMessage, status);
                                return;

                            case Status.CopyingError:
                            case Status.MiscellaneousError:
                            case Status.ProjectLoadingError:
                            case Status.BuildError:
                            case Status.UnhandledException:
                                string text = $"{status.ToTitle()}{(commitMessage == null ? "" : $" for commit '{commitMessage}'")}: {message}";
                                Logger.Log(text);
                                overallStatus = TestResult.Failure;

                                icon.Percentage = 1;
                                icon.Text = text;
                                icon.ShowErrorBalloon(message, status);
                                return;

                            case Status.Success:
                                Logger.Log("OK: " + message);
                                icon.Percentage = 1;
                                icon.Text = $"Done. {successfulTestsCount} tests successful";
                                icon.Status = NotificationIconStatus.Ok;
                                overallStatus = TestResult.Success;
                                break;

                            case Status.ParentFailed:
                            case Status.ArgumentError:
                                throw new ContractException("This error status should have been returned by the prework already");
                            default:
                                throw new DefaultSwitchCaseUnreachableException();
                        }
                    }
                }
                catch (Exception e)
                {
                    overallStatus = TestResult.Failure;
                    Logger.Log($"{Status.UnhandledException.ToTitle()}: " + e.Message);
                    throw new ContractException(e.StackTrace);
                }
                finally
                {
                    icon.Percentage = 1; // removes the cancel button
                    icon.CancellationRequested -= onOperationCanceled;
                    if (icon.Status == NotificationIconStatus.Working)
                        icon.Status = NotificationIconStatus.Ok;

                    if (work.Hash != null && resultsFile != null && overallStatus != TestResult.Ignored)
                    {
                        try
                        {
                            resultsFile.Append(work.Hash, overallStatus, commitMessage, (int)Math.Ceiling((DateTime.Now - start).TotalSeconds), successfulTestsCount);
                        }
                        catch { }
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
        }

        void IDisposable.Dispose()
        {
            this.icon?.Dispose();
        }
    }
}