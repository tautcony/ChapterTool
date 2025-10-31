// ****************************************************************************
// Public Domain
// code from http://sourceforge.net/projects/gmkvextractgui/
// ****************************************************************************
namespace ChapterTool.Util
{
    using System;
    using System.Text;

    public static class Logger
    {
        private static readonly StringBuilder LogContext = new StringBuilder();

        public static string LogText => LogContext.ToString();

        public static event Action<string, DateTime> LogLineAdded;

        public static void Log(string message)
        {
            var actionDate = DateTime.Now;
            string logMessage = $"{actionDate:[yyyy-MM-dd][HH:mm:ss]} {message}";
            LogContext.AppendLine(logMessage);
            OnLogLineAdded(logMessage, actionDate);
        }

        private static void OnLogLineAdded(string lineAdded, DateTime actionDate)
        {
            LogLineAdded?.Invoke(lineAdded, actionDate);
        }
    }
}
