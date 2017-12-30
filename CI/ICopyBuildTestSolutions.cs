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
        IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTestsDelegate(CancellationToken cancellationToken, 
                                                                                         out TestResultsFile resultsFile,
                                                                                         out string commitMessage,
                                                                                         out int projectCount);
    }
    public sealed class CopyBuildTestSolutions : ICopyBuildTestSolutions
    {
        public string SolutionFilePath { get; }
        public string BaseDestinationDirectory { get; }
        public string Hash { get; }
        public CopyBuildTestSolutions(string solutionFilePath, string baseDestinationDirectory, string hash)
        {
            this.SolutionFilePath = solutionFilePath;
            this.BaseDestinationDirectory = baseDestinationDirectory;
            this.Hash = hash;
        }
        public IEnumerable<(Status Status, string Message)> CopySolutionAndExecuteTestsDelegate(CancellationToken cancellationToken, out TestResultsFile resultsFile, out string commitMessage, out int projectCount)
        {
            return Program.CopySolutionAndExecuteTests(SolutionFilePath, BaseDestinationDirectory, out resultsFile, out commitMessage, out projectCount, Hash, cancellationToken);
        }
    }
}
