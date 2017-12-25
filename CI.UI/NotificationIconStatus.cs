using JBSnorro.Diagnostics;
using System;

namespace CI.UI
{
    /// <summary>
    /// Represents the status to be displayed by a notification icon.
    /// </summary>
    public sealed class NotificationIconStatus
    {
        public static readonly NotificationIconStatus Default = new NotificationIconStatus();
        public static readonly NotificationIconStatus Ok = new NotificationIconStatus();
        public static readonly NotificationIconStatus Bad = new NotificationIconStatus();

        public static NotificationIconStatus Working(float percentage = 0) => new NotificationIconStatus(percentage);

        internal float WorkingPercentage
        {
            get
            {
                Contract.Requires<InvalidOperationException>(this != Default && this != Ok && this != Bad, "Can only obtain percentage of working status");

                return this._percentage;
            }
        }
        private readonly float _percentage;

        private NotificationIconStatus() { }
        private NotificationIconStatus(float percentage)
        {
            Contract.Requires(0 <= percentage);
            Contract.Requires(percentage <= 1);

            this._percentage = percentage;
        }

        /// <summary>
        /// Equality is defined without percentage.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (this != Default && this != Ok && this != Bad)
                return obj != Default && obj != Ok && obj != Bad;
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            if (this == Default)
                return 0;
            if (this == Ok)
                return 1;
            if (this == Bad)
                return 2;
            return 3; // Equality is defined without percentage
        }
    }
}
