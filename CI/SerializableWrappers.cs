using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// Serves as a wrapper type for propagating assembly paths across app domains.
    /// </summary>
    [Serializable]
    sealed class SerializableProjectAssemblyPaths
    {
        public string[] ProjectAssemblyPathsInBuildOrder { get; }

        public SerializableProjectAssemblyPaths(string[] projectAssemblyPathsInBuildOrder)
        {
            this.ProjectAssemblyPathsInBuildOrder = projectAssemblyPathsInBuildOrder;
        }
        public static implicit operator SerializableProjectAssemblyPaths(string[] projectAssemblyPathsInBuildOrder)
        {
            return new SerializableProjectAssemblyPaths(projectAssemblyPathsInBuildOrder);
        }
    }
    /// <summary>
    /// Serves as a wrapper type for propagating test results across app domains.
    /// </summary>
    [Serializable]
    sealed class SerializableTestResults
    {
        public int TotalTestCount { get; }
        public string Error { get; }

        public SerializableTestResults(int totalTestCount, string error)
        {
            this.TotalTestCount = totalTestCount;
            this.Error = error;
        }
        public static implicit operator SerializableTestResults((int totalTestCount, string error) t)
        {
            return new SerializableTestResults(t.totalTestCount, t.error);
        }
        public void Deconstruct(out int totalTestCount, out string error)
        {
            totalTestCount = this.TotalTestCount;
            error = this.Error;
        }
    }
}
