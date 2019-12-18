using JBSnorro.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JBSnorro.GitTools
{
	public interface IActiveWindowTitle
	{
		string GetActiveWindowTitle();
	}
	public sealed class ActiveWindowTitleGetter : IActiveWindowTitle
	{
		string IActiveWindowTitle.GetActiveWindowTitle() => WindowTitleExtensions.GetActiveWindowTitle();
	}
	public static class WindowTitleExtensions
	{
		/// <summary>
		/// Gets the title of the active window.
		/// </summary>
		public static string GetActiveWindowTitle()
		{
			IntPtr handle = GetForegroundWindow();

			return handle.GetWindowText();
		}

		private static string GetWindowText(this IntPtr handle)
		{
			Contract.Requires(handle != IntPtr.Zero);

			const int nChars = 256;
			StringBuilder buffer = new StringBuilder(nChars);

			if (GetWindowText(handle, buffer, nChars) > 0)
			{
				return buffer.ToString();
			}
			return null;
		}
		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

	}
}
