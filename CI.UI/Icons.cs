﻿using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CI.UI
{
    /// <summary>
    /// Handles loading/retrieving icons.
    /// </summary>
    public static class Icons
    {
        private static string executingAssemblyDirectory => Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBa‌​se).LocalPath);
        private static string iconsPath => Path.Combine(executingAssemblyDirectory, ConfigurationManager.AppSettings["iconsPath"] ?? throw new ContractException("AppSetting 'iconsPath' not found"));
        private static Dictionary<NotificationIconStatus, Icon> icons = new Dictionary<NotificationIconStatus, Icon>
        {
            [NotificationIconStatus.Default] = Convert(Path.Combine(iconsPath, "default_status.png")),
            [NotificationIconStatus.Ok] = Convert(Path.Combine(iconsPath, "ok_status.png")),
            [NotificationIconStatus.Working] = Convert(Path.Combine(iconsPath, "working_status.png")),
            [NotificationIconStatus.Bad] = Convert(Path.Combine(iconsPath, "bad_status.png")),
        };

        /// <summary>
        /// Gets the icon representing the specified status/
        /// </summary>
        public static Icon GetIcon(NotificationIconStatus status)
        {
            Contract.RequiresEnumIsDefined(status);

            return icons[status];
        }

        private static Icon Convert(string file)
        {
            using (var stream = File.OpenRead(file))
                return Convert(stream);
        }
        private static Icon Convert(Stream iconImage)
        {
            Bitmap bitmap = (Bitmap)Image.FromStream(iconImage);
            return Icon.FromHandle(bitmap.GetHicon());
        }
    }
}
