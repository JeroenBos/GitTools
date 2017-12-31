using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
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
        private static readonly Bitmap reusableBitmap = (Bitmap)bitmaps[NotificationIconStatus.Working].Clone();

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
        private static Bitmap GetBitmap(NotificationIconStatus status)
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

        /// <summary>
        /// Gets the overlap of the icons of the specified statusses.
        /// </summary>
        /// <param name="backgroundStatus"> The status used as background icon. </param>
        /// <param name="foregroundStatus"> The status used as foreground icon. </param>
        /// <param name="percentage"> The percentage of the drawn foreground icon. </param>
        public static Icon GetIcon(NotificationIconStatus backgroundStatus, NotificationIconStatus foregroundStatus, double percentage)
        {
            Contract.RequiresEnumIsDefined(backgroundStatus);
            Contract.RequiresEnumIsDefined(foregroundStatus);
            Contract.Requires(0 <= percentage && percentage <= 1);

            if (percentage == 0)
                return GetIcon(backgroundStatus);
            if (percentage == 1)
                return GetIcon(foregroundStatus);

            Bitmap background = GetBitmap(backgroundStatus);
            Bitmap foreground = GetBitmap(foregroundStatus);

            Contract.Assume(foreground.Width == background.Width);
            Contract.Assume(foreground.Height == background.Height);

            int height = foreground.Height;
            int width = foreground.Width;
            int y = (int)Math.Floor(percentage * height);
            int x = (int)Math.Floor((percentage * height - y) * width);
            using (Graphics g = Graphics.FromImage(reusableBitmap))
            {
                g.DrawImageUnscaled(background, default(Point));
                var block = new Rectangle(0, height - y, foreground.Width, foreground.Height);
                var partialLine = new Rectangle(0, height - y - 1, x, 1);
                g.DrawImage(foreground, block, block, GraphicsUnit.Pixel);
                g.DrawImage(foreground, partialLine, partialLine, GraphicsUnit.Pixel);
            }

            return Convert(reusableBitmap);
        }
    }
}
