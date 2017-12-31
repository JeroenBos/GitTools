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
        ProjectLoadSuccess,
        BuildSuccess,
        TestStarted,
        TestSuccess,
        ArgumentError,
        MiscellaneousError,
        ProjectLoadingError,
        BuildError,
        TestError,
        UnhandledException,
        Skipped,
        Canceled,
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
                case Status.ProjectLoadSuccess:
                    return "Project load successful";
                case Status.BuildSuccess:
                    return "Build successful";
                case Status.TestSuccess:
                    return "Test successful";
                case Status.ArgumentError:
                    return "Input invalid";
                case Status.MiscellaneousError:
                    return "Unknown error";
                case Status.ProjectLoadingError:
                    return "Loading project failed";
                case Status.BuildError:
                    return "Build failed";
                case Status.TestStarted:
                    return "Test started";
                case Status.TestError:
                    return "Tests failed";
                case Status.UnhandledException:
                    return "Unhandled exception occurred";
                case Status.Skipped:
                    return "Build and test skipped";
                case Status.Canceled:
                    return "Canceled";
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
        /// <summary>
        /// Gets whether the specified status is considered a success.
        /// </summary>
        public static bool IsSuccessful(this Status status)
        {
            switch (status)
            {
                case Status.Success:
                case Status.ProjectLoadSuccess:
                case Status.BuildSuccess:
                case Status.TestSuccess:
                case Status.Skipped:
                case Status.TestStarted:
                    return true;
                case Status.ArgumentError:
                case Status.MiscellaneousError:
                case Status.ProjectLoadingError:
                case Status.BuildError:
                case Status.TestError:
                case Status.UnhandledException:
                case Status.Canceled:
                    return false;
                default:
                    throw new DefaultSwitchCaseUnreachableException();
            }
        }
    }
}
