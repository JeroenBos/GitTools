using JBSnorro;
using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;

namespace CI.UI
{
    /// <summary>
    /// Represents the status to be displayed by a notification icon.
    /// </summary>
    public sealed class NotificationIconStatus
    {
        public static IEqualityComparer<NotificationIconStatus> EqualityComparerIncludingText = InterfaceWraps.ToEqualityComparer<NotificationIconStatus>((left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.IsWorking && right.IsWorking && left.WorkingHoverText == right.WorkingHoverText));
        public static readonly NotificationIconStatus Default = new NotificationIconStatus();
        public static readonly NotificationIconStatus Ok = new NotificationIconStatus();
        public static readonly NotificationIconStatus Bad = new NotificationIconStatus();

        public static NotificationIconStatus Working(float percentage = 0, string hoverText = null) => new NotificationIconStatus(percentage, hoverText);

        public float WorkingPercentage
        {
            get
            {
                Contract.Requires<InvalidOperationException>(this.IsWorking, "Can only obtain percentage of working status");

                return this._percentage;
            }
        }
        public string WorkingHoverText
        {
            get
            {
                Contract.Requires<InvalidOperationException>(this.IsWorking, "Can only obtain percentage of working status");

                return this._text;
            }
        }
        public bool IsWorking => this != Default && this != Ok && this != Bad;
        private readonly float _percentage;
        private readonly string _text;

        private NotificationIconStatus() { }
        private NotificationIconStatus(float percentage, string text)
        {
            Contract.Requires(0 <= percentage);
            Contract.Requires(percentage <= 1);

            this._percentage = percentage;
            this._text = text;
        }

        /// <summary>
        /// Equality is defined without percentage.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (this.IsWorking)
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
