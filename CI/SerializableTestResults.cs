using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// Serves as a wrapper type for propagating test results across app domains.
    /// </summary>
    [Serializable]
    sealed class SerializableTestResults
    {
        public int SuccessfulTestCount { get; }
        public int TotalTestCount { get; }
        public string Error { get; }

        public SerializableTestResults(int totalTestCount, int successfulTestCount)
        {
            this.SuccessfulTestCount = successfulTestCount;
            this.TotalTestCount = totalTestCount;
        }
        public SerializableTestResults(string error)
        {
            this.SuccessfulTestCount = -1;
            this.TotalTestCount = -1;
            this.Error = error;
        }
    }
}
