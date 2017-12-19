using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    public static class Logger
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
            File.AppendAllText("D:\\tmp\\log.txt", DateTime.Now.ToString("hh:mm:ss") + " " + message + "\r\n"); 
        }
    } 
}
