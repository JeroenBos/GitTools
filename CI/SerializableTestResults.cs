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
        public int TotalTestCount { get; }
        public string Error { get; }

        public SerializableTestResults(int totalTestCount)
        {
            this.TotalTestCount = totalTestCount;
        }
        public SerializableTestResults(string error)
        {
            this.TotalTestCount = int.MinValue;
            this.Error = error;
        }
    }
}
