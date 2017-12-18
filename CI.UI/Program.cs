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

namespace CI.UI
{
    public class Program
    {
        private readonly static NotificationIcon icon = new NotificationIcon();

        static void Main(string[] args)
        {
            if (args.Length != 0)
                throw new ArgumentException("Legacy direct call deprecated; call via CI.Dispatcher");

            OutputError(() => ReceivingPipe.Start());
        }
        public static void OutputError(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                OutputError(e);
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
            Debug.WriteLine(e.StackTrace);
            Debug.WriteLine(e.Message);
            Console.WriteLine(e.Message);
#if DEBUG
            icon.Status = NotificationIconStatus.Bad;
            Console.ReadLine();
#else
            icon.ShowErrorBalloon(e.Message, e is ArgumentException ? Status.ArgumentError : Status.UnhandledException);
            Thread.Sleep(NotificationIcon.ErrorBalloonShowDuration);
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

            icon.Status = NotificationIconStatus.Working;
            var (status, message) = JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(solutionFilePath, ConfigurationManager.AppSettings["destinationDirectory"], hash);

            if (status == Status.Success)
            {
                icon.Status = NotificationIconStatus.Ok;
            }
            else if (status == Status.Skipped)
            {
                icon.Status = NotificationIconStatus.Default;
            }
            else
            {
                Console.WriteLine(message);
                icon.ShowErrorBalloon(message, status);
            }
        }
    }
}
