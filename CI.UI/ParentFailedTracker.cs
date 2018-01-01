using JBSnorro;
using JBSnorro.Diagnostics;
using JBSnorro.GitTools;
using JBSnorro.GitTools.CI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CI.UI
{
    /// <summary>
    /// Tracks all messages that weren't handled because their parents failed.
    /// </summary>
    internal static class ParentFailedTracker
    {
        private static readonly TimeSpan expirationTime = TimeSpan.FromMilliseconds(ConfigurationManagerExtensions.ParseAppSettingInt("ParentFailedTrackerExpirationTime_ms", 60000));
        private readonly static List<(ICopyBuildTestSolutions, DateTime)> canceledMessagesBecauseParentFailed = new List<(ICopyBuildTestSolutions, DateTime)>();
        internal static int Count => canceledMessagesBecauseParentFailed.Count;

        /// <summary>
        /// Rehandles all messages that were canceled because the parent commits had failed, but now disregarding that fact.
        /// </summary>
        internal static void RedoCanceledMessagesBecauseParentFailed(NotificationIcon icon)
        {
            Contract.Requires(icon != null);
            if (!TestClassExtensions.IsRunningFromUnitTest && Program.Dispatcher != Dispatcher.CurrentDispatcher)
            {
                Program.Dispatcher.InvokeAsync(() => RedoCanceledMessagesBecauseParentFailed(icon));
                return;
            }

            icon.Reset();
            var copy = canceledMessagesBecauseParentFailed.ToArray();
            canceledMessagesBecauseParentFailed.Clear();

            foreach ((ICopyBuildTestSolutions canceledWork, DateTime timeStamp) in copy)
            {
                if (DateTime.Now - timeStamp > expirationTime)
                    continue;

                Program.HandleCommit(canceledWork, icon, ignoreParentFailed: true); //external cancellation token is already linked to the icon cancellation event, so therefore it's not here
            }
        }
        /// <summary>
        /// Adds work to this tracker.
        /// </summary>
        public static void Add(ICopyBuildTestSolutions work)
        {
            canceledMessagesBecauseParentFailed.Add((work, DateTime.Now));
        }

        /// <summary>
        /// For in between unit tests.
        /// </summary>
        internal static void Clear()
        {
            canceledMessagesBecauseParentFailed.Clear();
        }
    }
}
