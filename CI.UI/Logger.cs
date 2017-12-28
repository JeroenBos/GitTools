using JBSnorro.Diagnostics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(LogPath), "unableToLog.txt"), DateTime.Now.ToString("hh: mm:ss") + " " + e.Message + "\r\n");
                }
            }

            void log(string m)
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("hh:mm:ss") + " " + m + "\r\n");
            }
        }
    }
}
