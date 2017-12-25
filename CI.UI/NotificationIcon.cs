using JBSnorro;
using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JBSnorro.GitTools.CI;
using System.Windows.Threading;

namespace CI.UI
{
    /// <summary>
    /// Wraps around the notification area.
    /// </summary>
    public sealed class NotificationIcon : DefaultINotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Gets the duration to show the error balloon in ms.
        /// </summary>
        public const int ErrorBalloonShowDuration = 5000;

        private NotifyIcon Icon { get; }
        /// <summary>
        /// Gets or sets the status of the notification icon.
        /// </summary>
        public NotificationIconStatus Status
        {
            get => this._status;
            set => base.Set(ref this._status, value);
        }

        /// <summary>
        /// Creates a new nofication icon in the notification area.
        /// </summary>
        public NotificationIcon()
        {
            ProcessExit.Event += onProcessExit;
            this.Icon = new NotifyIcon()
            {
                Visible = true,
                ContextMenu = new ContextMenu(new[] { new MenuItem("Exit", (sender, e) => Dispatcher.CurrentDispatcher.InvokeShutdown()) })
            };

            this.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Status)) OnStatusChanged();
            };

            this.Status = NotificationIconStatus.Default;
        }

        private void OnStatusChanged()
        {
            this.Icon.Icon = Icons.GetIcon(this.Status);
        }

        /// <summary>
        /// Shows an error message in a balloon.
        /// </summary>
        public void ShowErrorBalloon(string message, Status status)
        {
            Contract.RequiresEnumIsDefined(status);
            Contract.Requires(status != JBSnorro.GitTools.CI.Status.Success);

            this.Status = NotificationIconStatus.Bad;

            this.Icon.ShowBalloonTip(ErrorBalloonShowDuration, status.ToTitle(), message, ToolTipIcon.Error);
        }

        public void Dispose()
        {
            this.Icon.Dispose();
            ProcessExit.Event -= onProcessExit;
        }

        private bool onProcessExit(ProcessExit.EventArg exitType)
        {
            Logger.Log("Exiting UI");
            this.Dispose();
            return true;
        }

        private NotificationIconStatus _status;
    }
}
