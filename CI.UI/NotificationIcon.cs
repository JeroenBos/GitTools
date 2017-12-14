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

namespace CI.UI
{
    /// <summary>
    /// Wraps around the notification area.
    /// </summary>
    public sealed class NotificationIcon : DefaultINotifyPropertyChanged, IDisposable
    {
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
            this.Icon = new NotifyIcon()
            {
                Visible = true,
            };

            this.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(Status)) OnStatusChanged(); };

            //set default icon
            this.OnStatusChanged();
        }

        private void OnStatusChanged()
        {
            this.Icon.Icon = Icons.GetIcon(this.Status);
        }

        /// <summary>
        /// Shows an error message in a balloon.
        /// </summary>
        public void ShowErrorBalloon(string message, JBSnorro.GitTools.CI.Status status)
        {
            this.Status = NotificationIconStatus.Bad;

            this.Icon.ShowBalloonTip(5000, JBSnorro.GitTools.CI.StatusExtensions.ToDisplayString(status), message, ToolTipIcon.Error);
        }
        
        public void Dispose()
        {
            this.Icon.Visible = false;
        }

        private NotificationIconStatus _status;
    }
}
