using JBSnorro;
using JBSnorro.Extensions;
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
using System.Threading;
using Timer = System.Threading.Timer;
using System.Configuration;

namespace CI.UI
{
    /// <summary>
    /// Wraps around the notification area.
    /// </summary>
    public sealed class NotificationIcon : DefaultINotifyPropertyChanged, IDisposable
    {
        public bool HasCancellationRequestedHandler => this.cancellationRequested != null;
        public event EventHandler CancellationRequested
        {
            add
            {
                this.cancellationRequested += value;
            }
            remove
            {
                this.cancellationRequested -= value;
            }
        }
        private EventHandler cancellationRequested;


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
            set => base.Set(ref _status, value);
        }
        /// <summary>
        /// Gets or sets the estimated percentage of the operation.
        /// </summary>
        public double Percentage
        {
            get => _percentage;
            set
            {
                Contract.Requires(0 <= value && value <= 1);

                base.Set(ref _percentage, value);
            }
        }
        /// <summary>
        /// Gets or sets the text displayed when hovering over the notification icon.
        /// </summary>
        public string Text
        {
            get => _text;
            set => base.Set(ref _text, value);
        }
        /// <summary>
        /// Gets whether this <see cref="NotificationIcon"/> has been disposed of.
        /// </summary>
        public bool IsDisposed { get; private set; }
        /// <summary>
        /// Represents the timer that resets the icon after a certain time of inactivity.
        /// </summary>
        private readonly (Timer timer, TimeSpan span) resetTimer;

        internal NotificationIconContextMenuItems ContextMenuItems
        {
            get => NotificationIconContextMenus.From(this.Icon.ContextMenu);
            set
            {
                if (value != this.ContextMenuItems)
                {
                    this.Icon.ContextMenu = NotificationIconContextMenus.Create(value, this);
                }
            }
        }

        /// <summary>
        /// Creates a new nofication icon in the notification area.
        /// </summary>
        public NotificationIcon(bool isVisible = false)
        {
            ProcessExit.Event += onProcessExit;
            this.Icon = new NotifyIcon()
            {
                Visible = isVisible,
            };

            this.Status = NotificationIconStatus.Default;
            this.Percentage = 1;
            this.resetTimer = createTimer();

            this.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Status)) OnStatusChanged();
                if (e.PropertyName == nameof(Text)) { this.Icon.Text = this.Text; }
                if (e.PropertyName == nameof(Percentage)) OnPercentageChanged();
                this.resetTimer.timer?.Change(this.resetTimer.span, TimeSpan.FromMilliseconds(-1));
            };

            OnStatusChanged(); //sets exit button and default icon

            (Timer, TimeSpan) createTimer()
            {
                const string settingName = "NotificationIconReset_ms";
                string rawSetting;
                if (!isVisible || (rawSetting = ConfigurationManager.AppSettings[settingName]) == null)
                    return (null, default(TimeSpan)); // icon only resets if the setting is present

                if (!int.TryParse(rawSetting, out int setting_ms))
                {
                    throw new InvalidAppSettingException(settingName, "A number was expected. ");
                }

                return (new Timer(_ => this.Reset(), null, setting_ms, -1), TimeSpan.FromMilliseconds(setting_ms));
            }
        }

        private void OnStatusChanged()
        {
            var icon = Icons.GetIcon(this.Status);
            Contract.Assert(icon != null);
            try
            {
                this.Icon.Icon = icon;
            }
            catch (ArgumentNullException)
            {
                Thread.Sleep(10);
                this.Icon.Icon = icon;
            }

            RefreshContextMenuItems();
        }
        private void OnPercentageChanged()
        {
            RefreshContextMenuItems();
            SetPartialIcon();
        }
        private void RefreshContextMenuItems()
        {
            bool menuShouldHaveCancel = this.Percentage != 1;
            if (menuShouldHaveCancel)
            {
                this.ContextMenuItems = NotificationIconContextMenuItems.Exit | NotificationIconContextMenuItems.Cancel;
            }
            else
            {
                this.ContextMenuItems = NotificationIconContextMenuItems.Exit;
            }
        }
        private void SetPartialIcon()
        {
            if (this.Status == NotificationIconStatus.Working)
            {
                //TODO: find out if the icon must be disposed of (which it should if it doesn't dispose of the bitmap it's created from)
                // For non-reusable icons, it should never
                this.Icon.Icon = Icons.GetIcon(NotificationIconStatus.Working, NotificationIconStatus.Ok, this.Percentage);
            }
        }

        /// <summary>
        /// Shows an error message in a balloon.
        /// </summary>
        public void ShowErrorBalloon(string message, Status status)
        {
            Contract.RequiresEnumIsDefined(status);
            Contract.Requires(status != JBSnorro.GitTools.CI.Status.Success);
            Contract.Requires(!string.IsNullOrEmpty(message));

            this.Status = NotificationIconStatus.Bad;

            this.Icon.ShowBalloonTip(ErrorBalloonShowDuration, status.ToTitle(), message, ToolTipIcon.Error);
        }
        /// <summary>
        /// Sets this icon back to the default state.
        /// </summary>
        public void Reset()
        {
            if (this.IsDisposed)
                throw new ObjectDisposedException(nameof(NotificationIcon));

            this.Text = null;
            this.Percentage = 1;
            this.Status = NotificationIconStatus.Default;
        }

        internal void RequestCancellation()
        {
            this.cancellationRequested?.Invoke(this, new EventArgs());
        }
        public void Dispose()
        {
            IsDisposed = true;
            this.resetTimer.timer?.Dispose();
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
        private double _percentage;
        private string _text;
    }
}
