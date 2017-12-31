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
        public TestResultsFile ResultsFile { get; }
        public string CommitMessage { get; }
        public int ProjectCount { get; }

        public MockCopyBuildTestSolutions(Func<IEnumerable<(Status, string)>> getLog, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0)
        {
            this.getLog = getLog;
            this.ResultsFile = resultsFile;
            this.CommitMessage = commitMessage;
            this.ProjectCount = projectCount;
        }
        public MockCopyBuildTestSolutions(IEnumerable<(Status, string)> log, TestResultsFile resultsFile = null, string commitMessage = null, int projectCount = 0)
            : this(() => log, resultsFile, commitMessage, projectCount)
        {

        }


        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount)
        {
            projectCount = this.ProjectCount;
            return getLog();
        }

        public Prework Prework()
        {
            return new Prework(Status.Success, null);
        }
    }
}
