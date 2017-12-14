using JBSnorro.Diagnostics;
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

    public static class StatusExtensions
    {
        public static string ToDisplayString(this Status status)
        {
            Contract.RequiresEnumIsDefined(status);

            switch (status)
            {
                case Status.Success:
                    return "Tests run successfully";
                case Status.ArgumentError:
                    return "Input invalid";
                case Status.MiscellaneousError:
                    return "Unknown error";
                case Status.BuildError:
                    return "Build failed";
                case Status.TestError:
                    return "Tests failed";
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
    }
}
