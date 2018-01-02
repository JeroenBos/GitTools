using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private Option<bool> preworkDoneArgument;
        private Prework prework;

        [DebuggerHidden]
        public Prework Prework(bool ignoreParentFailed)
        {
            Contract.Requires(!preworkDoneArgument.HasValue || (!preworkDoneArgument.Value && ignoreParentFailed), "You can only call this method once with true, and only once with false before the call with true");
            preworkDoneArgument = ignoreParentFailed;

            this.prework = JBSnorro.GitTools.CI.Program.Prework(SolutionFilePath, BaseDestinationDirectory, Hash, ignoreParentFailed);
            return this.prework;
        }

        [DebuggerHidden]
        public CopyBuildTestSolutionInjection(string solutionFilePath, string baseDestinationDirectory, string hash)
        {
            Contract.Requires(solutionFilePath != null, nameof(solutionFilePath));
            Contract.Requires(baseDestinationDirectory != null, "The appsetting key 'destinationDirectory' was not found");

            this.SolutionFilePath = solutionFilePath;
            this.BaseDestinationDirectory = baseDestinationDirectory;
            this.Hash = hash;
        }
        [DebuggerHidden]
        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTests(CancellationToken cancellationToken, out int projectCount)
        {
            Contract.Requires(preworkDoneArgument.HasValue, "prework must be called before calling this method");

            return JBSnorro.GitTools.CI.Program.CopySolutionAndExecuteTests(SolutionFilePath, prework.DestinationDirectory, out projectCount, Hash, cancellationToken);
        }
    }
}
