using JBSnorro.Diagnostics;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace JBSnorro.GitTools.CI
{
	public static class Logger
	{
		public static readonly string LogPath = ConfigurationManager.AppSettings["logPath"] ?? throw new AppSettingNotFoundException("logPath");
		[DebuggerHidden]
		public static void Log(string message)
		{
			Console.WriteLine(message);
			try
			{
				log(message);
			}
			catch
			{
				Thread.Sleep(10);
				try
				{
					log(message);
				}
				catch (Exception e)
				{
					try
					{
						logAt(Path.Combine(Path.GetDirectoryName(LogPath), "unableToLog.txt"), e.Message);
					}
					catch { }
				}
			}

			void log(string m)
			{
				logAt(LogPath, message);
			}
			void logAt(string path, string m)
			{
				try
				{
					File.AppendAllText(path, DateTime.Now.ToString("hh:mm:ss.ff") + " " + (TestClassExtensions.RunningTestMethodName ?? "") + " " + m + "\r\n");
				}
				catch { }
			}
		}
	}
}
