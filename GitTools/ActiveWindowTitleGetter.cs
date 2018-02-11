using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools
{
    public interface IActiveWindowTitle
    {
        string GetActiveWindowTitle();
    }
    public sealed class ActiveWindowTitleGetter : IActiveWindowTitle
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buffer = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buffer, nChars) > 0)
            {
                return buffer.ToString();
            }
            return null;
        }

        string IActiveWindowTitle.GetActiveWindowTitle() => GetActiveWindowTitle();
    }
}
