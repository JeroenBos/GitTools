using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Drawing;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            [NotificationIconStatus.Default] = ReadImage("default_status.png"),
            [NotificationIconStatus.Ok] = ReadImage("ok_status.png"),
            [NotificationIconStatus.Working] = ReadImage("working_status.png"),
            [NotificationIconStatus.Bad] = ReadImage("bad_status.png"),
            [NotificationIconStatus.BadParent] = ReadImage("bad_status.png"),
        });
        private static readonly ReadOnlyDictionary<NotificationIconStatus, Icon> icons = new ReadOnlyDictionary<NotificationIconStatus, Icon>(bitmaps.ToDictionary(kvp => kvp.Key, kvp => Convert(kvp.Value)));
        private static readonly Bitmap reusableBitmap = (Bitmap)bitmaps[NotificationIconStatus.Working].Clone();
        private static Bitmap ReadImage(string filename)
        {
            Contract.Requires(!string.IsNullOrEmpty(filename));

            string path = Path.GetFullPath(Path.Combine(iconsPath, filename));
            Contract.Requires(File.Exists(path), $"Could not find file '{path}'");

            //we're cloning so that we can release the file handle
            using (var image = new Bitmap(path))
            {
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// Gets the icon representing the specified status.
        /// </summary>
        public static Icon GetIcon(NotificationIconStatus status)
        {
            Contract.RequiresEnumIsDefined(status);

            var result = icons[status];
            Contract.Ensures(result != null, "Icon was null");
            return result;
        }
        /// <summary>
        /// Gets the icon representing the specified status.
        /// </summary>
        private static Bitmap GetBitmap(NotificationIconStatus status)
        {
            Contract.RequiresEnumIsDefined(status);

            return bitmaps[status];
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
        /// <summary>
        /// Releases the icon if it is not going to be reused any more.
        /// </summary>
        public static void ReleaseIfNecessary(Icon icon)
        {
            if (icon == null)
                return;

            IEnumerable<Icon> reusableIcons = icons.Values;
            if (!reusableIcons.Contains(icon))
            {
                icon.Dispose();
                if (icon.Handle != IntPtr.Zero)
                {
                    DestroyIcon(icon.Handle);
                }
            }
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private extern static bool DestroyIcon(IntPtr handle);
    }
}
