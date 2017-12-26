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
using System.ComponentModel;

namespace CI.UI
{
    /// <summary>
    /// Wraps around the notification area.
    /// </summary>
    public sealed class NotificationIcon : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

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
            set
            {
                if (!NotificationIconStatus.EqualityComparerIncludingText.Equals(this._status, value))
                {
                    this._status = value;
                    this.PropertyChanged(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
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
            var status = this.Status;
            this.Icon.Icon = Icons.GetIcon(status);
            this.Icon.Text = status.IsWorking ? status.WorkingHoverText : null;
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
