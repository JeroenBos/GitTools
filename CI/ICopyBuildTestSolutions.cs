using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    public interface ICopyBuildTestSolutions
    {
        Prework Prework();
        IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount);
    }
    public sealed class Prework
    {
        public Status Status { get; }
        public string Message { get; }
        public string CommitMessage { get; }
        public TestResultsFile TestResultsFile { get; }
        public bool MustDoCheckout { get; }
        public string DestinationDirectory { get; }

        public bool HasError => Message != null;

        internal Prework(Status status, string message)
        {
            Contract.Requires(message != null || status == Status.Success);
            Contract.RequiresEnumIsDefined(status);

            this.Status = status;
            this.Message = message;
        }
        internal Prework(TestResultsFile resultsFile, string commitMessage, string destinationDirectory, bool mustDoCheckout)
        {
            Contract.Requires(resultsFile != null);

            this.Status = Status.Success;
            this.TestResultsFile = resultsFile;
            this.CommitMessage = commitMessage;
            this.MustDoCheckout = mustDoCheckout;
            this.DestinationDirectory = destinationDirectory;
        }
    }
}
