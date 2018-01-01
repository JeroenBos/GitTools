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

        public MockCopyBuildTestSolutions(Func<IEnumerable<(Status, string)>> getLog, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
        {
            this.getLog = getLog;
            this.ProjectCount = projectCount;
            this.prework = prework ?? new Prework(resultsFile, commitMessage, null, false);
            this.Hash = hash;
        }
        public MockCopyBuildTestSolutions(IEnumerable<(Status, string)> log, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0, string hash = null)
            : this(() => log, resultsFile, commitMessage, projectCount, hash)
        {
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, Func<IEnumerable<(Status, string)>> getLog, string hash = null)
        {
            Contract.Requires(getLog != null);
            Contract.Requires(preworkStatus.IsAnyOf(Status.ArgumentError, Status.Canceled, Status.MiscellaneousError, Status.ParentFailed, Status.Skipped, Status.UnhandledException));

            this.getLog = getLog;
            this.prework = new Prework(preworkStatus, "test error");
            this.Hash = hash;
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, IEnumerable<(Status, string)> log, string hash = null) : this(preworkStatus, () => log, hash) { }
        public MockCopyBuildTestSolutions(Status preworkStatus, string hash = null) : this(preworkStatus, () => throw new InvalidOperationException(), hash) { }



        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount)
        {
            projectCount = this.ProjectCount;
            return getLog();
        }

        public Prework Prework()
        {
            return this.prework;
        }
    }
}

