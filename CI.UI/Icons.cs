using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private static string iconsPath => Path.Combine(executingAssemblyDirectory, ConfigurationManager.AppSettings["iconsPath"] ?? throw new AppSettingNotFoundException("iconsPath"));
        private static readonly ReadOnlyDictionary<NotificationIconStatus, Bitmap> bitmaps = new ReadOnlyDictionary<NotificationIconStatus, Bitmap>(new Dictionary<NotificationIconStatus, Bitmap>
        {
            [NotificationIconStatus.Default] = (Bitmap)Image.FromFile(Path.Combine(iconsPath, "default_status.png")),
            [NotificationIconStatus.Ok] = (Bitmap)Image.FromFile(Path.Combine(iconsPath, "ok_status.png")),
            [NotificationIconStatus.Working] = (Bitmap)Image.FromFile(Path.Combine(iconsPath, "working_status.png")),
            [NotificationIconStatus.Bad] = (Bitmap)Image.FromFile(Path.Combine(iconsPath, "bad_status.png")),
        });
        private static readonly ReadOnlyDictionary<NotificationIconStatus, Icon> icons = new ReadOnlyDictionary<NotificationIconStatus, Icon>(bitmaps.ToDictionary(kvp => kvp.Key, kvp => Convert(kvp.Value)));

        /// <summary>
        /// Gets the icon representing the specified status.
        /// </summary>
        public static Icon GetIcon(NotificationIconStatus status)
        {
            Contract.RequiresEnumIsDefined(status);

            return icons[status];
        }
        /// <summary>
        /// Gets the icon representing the specified status.
        /// </summary>
        public static Bitmap GetBitmap(NotificationIconStatus status)
        {
            Contract.RequiresEnumIsDefined(status);

            return bitmaps[status];
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
        private static Icon Convert(Bitmap image)
        {
            return Icon.FromHandle(image.GetHicon());
        }
    }
}
