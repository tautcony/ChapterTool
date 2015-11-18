﻿// ****************************************************************************
// Public Domain
// code from http://sourceforge.net/projects/gmkvextractgui/
// ****************************************************************************
using System;
using System.Text;

namespace ChapterTool.Util
{
    public delegate void LogLineAddedEventHandler(string lineAdded, DateTime actionDate);
    // ReSharper disable once InconsistentNaming
    public class CTLogger
    {
        private static readonly StringBuilder LogContext = new StringBuilder();

        public static string LogText => LogContext.ToString();

        public static event LogLineAddedEventHandler LogLineAdded;

        public static void Log(string message)
        {
            DateTime actionDate = DateTime.Now;
            string logMessage = $"{actionDate.ToString("[yyyy-MM-dd][HH:mm:ss]")} {message}";
            LogContext.AppendLine(logMessage);
            OnLogLineAdded(logMessage, actionDate);
        }

        protected static void OnLogLineAdded(string lineAdded, DateTime actionDate)
        {
            LogLineAdded?.Invoke(lineAdded, actionDate);
        }
    }
}
