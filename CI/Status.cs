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
        TestSuccess,
        BuildSuccess,
        ArgumentError,
        MiscellaneousError,
        ProjectLoadingError,
        BuildError,
        TestError,
        UnhandledException,
        Skipped,
    }

    public static class StatusExtensions
    {
        public static string ToTitle(this Status status)
        {
            Contract.RequiresEnumIsDefined(status);

            switch (status)
            {
                case Status.Success:
                    return "All tests successful";
                case Status.TestSuccess:
                    return "Test successful";
                case Status.BuildSuccess:
                    return "Build successful";
                case Status.ArgumentError:
                    return "Input invalid";
                case Status.MiscellaneousError:
                    return "Unknown error";
                case Status.ProjectLoadingError:
                    return "Loading project failed";
                case Status.BuildError:
                    return "Build failed";
                case Status.TestError:
                    return "Tests failed";
                case Status.UnhandledException:
                    return "Unhandled exception occurred";
                case Status.Skipped:
                    return "Build and test skipped";
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
    }
}
