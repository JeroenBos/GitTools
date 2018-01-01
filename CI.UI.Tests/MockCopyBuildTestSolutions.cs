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
        private Prework prework { get; }

        public MockCopyBuildTestSolutions(Func<IEnumerable<(Status, string)>> getLog, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0)
        {
            this.getLog = getLog;
            this.ProjectCount = projectCount;
            this.prework = prework ?? new Prework(resultsFile, commitMessage, null, false);
        }
        public MockCopyBuildTestSolutions(IEnumerable<(Status, string)> log, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0)
            : this(() => log, resultsFile, commitMessage, projectCount)
        {
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, Func<IEnumerable<(Status, string)>> getLog)
        {
            Contract.Requires(getLog != null);
            Contract.Requires(preworkStatus.IsAnyOf(Status.ArgumentError, Status.Canceled, Status.MiscellaneousError, Status.ParentFailed, Status.Skipped, Status.UnhandledException));

            this.getLog = getLog;
            this.prework = new Prework(preworkStatus, "test error");
        }
        public MockCopyBuildTestSolutions(Status preworkStatus, IEnumerable<(Status, string)> log) : this(preworkStatus, () => log) { }
        public MockCopyBuildTestSolutions(Status preworkStatus) : this(preworkStatus, () => throw new InvalidOperationException()) { }



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

