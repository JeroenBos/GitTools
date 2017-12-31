using JBSnorro.Diagnostics;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CI.UI
{
    internal sealed class CopyBuildTestSolutionInjection : ICopyBuildTestSolutions
    {
        public string SolutionFilePath { get; }
        public string BaseDestinationDirectory { get; }
        public string Hash { get; }

        public bool HasDonePrework => this.prework != null;
        private Prework prework;

        public Prework Prework()
        {
            Contract.Requires(!HasDonePrework);

            this.prework = JBSnorro.GitTools.CI.Program.Prework(SolutionFilePath, BaseDestinationDirectory, Hash);
            return this.prework;
        }

        public CopyBuildTestSolutionInjection(string solutionFilePath, string baseDestinationDirectory, string hash)
        {
            this.SolutionFilePath = solutionFilePath;
            this.BaseDestinationDirectory = baseDestinationDirectory;
            this.Hash = hash;
        }
        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount)
        {
            Contract.Requires(HasDonePrework);

            return JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(SolutionFilePath, prework.DestinationDirectory, prework.MustDoCheckout, out projectCount, Hash, cancellationToken);
        }
    }
}
