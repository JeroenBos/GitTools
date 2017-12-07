using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    /// <summary>
    /// Represents the status of the copy/build/execute tests process.
    /// </summary>
    public enum Status
    {
        Success,
        ArgumentError,
        MiscellaneousError,
        BuildError,
        TestError
    }
}
