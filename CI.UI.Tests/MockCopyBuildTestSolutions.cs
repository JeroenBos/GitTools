using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CI.UI.Tests
{
    class MockCopyBuildTestSolutions : ICopyBuildTestSolutions
    {
        private readonly Func<IEnumerable<(Status, string)>> getLog;
        public int ProjectCount { get; }
        public string Hash { get; }
        private Prework prework { get; }
        private Prework secondPrework { get; }
        private Option<bool> preworkDoneArgument;

        public MockCopyBuildTestSolutions(Func<IEnumerable<(Status, string)>> getLog, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
        {
            this.getLog = getLog;
            this.ProjectCount = projectCount;
            this.prework = prework ?? new Prework(resultsFile, commitMessage, null);
            this.Hash = hash;
        }
        public MockCopyBuildTestSolutions(IEnumerable<(Status, string)> log, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
            : this(() => log, resultsFile, commitMessage, projectCount, hash)
        {
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, Func<IEnumerable<(Status, string)>> getLog, string hash = null, Status secondPreworkStatus = Status.None)
        {
            Contract.Requires(getLog != null);
            Contract.Requires(preworkStatus.IsAnyOf(Status.ArgumentError, Status.Canceled, Status.MiscellaneousError, Status.ParentFailed, Status.Skipped, Status.UnhandledException));
            Contract.Requires(secondPreworkStatus == Status.None || preworkStatus == Status.ParentFailed, "You can only specifiy second prework if the first is parent failed");

            this.getLog = getLog;
            this.prework = new Prework(preworkStatus, "test error");
            this.secondPrework = secondPreworkStatus == Status.None ? null : new Prework(secondPreworkStatus, "second test error");
            this.Hash = hash;
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, IEnumerable<(Status, string)> log, string hash = null, Status secondPreworkStatus = Status.None) : this(preworkStatus, () => log, hash, secondPreworkStatus) { }
        public MockCopyBuildTestSolutions(Status preworkStatus, string hash = null, Status secondPreworkStatus = Status.None) : this(preworkStatus, () => throw new InvalidOperationException(), hash, secondPreworkStatus) { }



        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount)
        {
            projectCount = this.ProjectCount;
            return getLog();
        }

        public Prework Prework(bool ignoreParentFailed)
        {
            Contract.Requires(!preworkDoneArgument.HasValue || (!preworkDoneArgument.Value && ignoreParentFailed), "You can only call this method once with true, and only once with false before the call with true");
            preworkDoneArgument = ignoreParentFailed;

            if (ignoreParentFailed)
            {
                Contract.Assert(this.secondPrework != null, "A second prework status should have been provided");
                return this.secondPrework;
            }
            return this.prework;
        }
    }
}

