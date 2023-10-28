﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zal
{
    public static class Logger
    {
        static readonly object _locker = new object();

        public static void Log(string logMessage)
        {
            try
            {
                var logFilePath = GetLogFilePath();
                //Use this for daily log files : "Log" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                WriteToLog(logMessage, logFilePath);
            }
            catch (Exception e)
            {
                //the irony, right? well i can't do much here
            }
        }
        public static String  GetLogFilePath()
        {
            return Path.Combine(Path.GetTempPath(), "zal_log.txt");

        }
        public static void ResetLog()
        {
            var logFilePath = GetLogFilePath();
            var lines = File.ReadAllLines(logFilePath);
            File.WriteAllLines(logFilePath, lines.Take(200).ToArray());
        }

        static void WriteToLog(string logMessage, string logFilePath)
        {
            lock (_locker)
            {
                string formattedDate = DateTime.Now.ToString("dd/MM/yyyy - HH:mm:ss");
                File.AppendAllText(logFilePath,
                        string.Format("Date: {1}{0}Msg: {2}{0}--------------------{0}",
                        Environment.NewLine, formattedDate, logMessage));
            }
        }
    }
}
