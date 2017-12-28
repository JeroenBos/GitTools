using System;
using System.Collections.Generic;
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
                    log("couldnt log before");
                    log(message);
                }
                catch
                {
                    File.WriteAllText("D:\\tmp\\unableTolog.txt", "");
                }
            }

            void log(string m)
            {
                File.AppendAllText("D:\\tmp\\log.txt", DateTime.Now.ToString("hh:mm:ss") + " " + m + "\r\n");
            }
        }
    }
}
