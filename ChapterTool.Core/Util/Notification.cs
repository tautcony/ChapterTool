// ****************************************************************************
//
// Copyright (C) 2014-2016 TautCony (TautCony@vcb-s.com)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************

namespace ChapterTool.Util
{
    /// <summary>
    /// Cross-platform notification placeholder
    /// UI layer should implement actual notification display
    /// </summary>
    public static class Notification
    {
        public enum NotificationType
        {
            Info,
            Warning,
            Error,
            Question
        }

        public enum NotificationResult
        {
            OK,
            Cancel,
            Yes,
            No
        }

        // Event that UI layer can subscribe to
        public static event Action<string, string, NotificationType>? OnNotification;

        // Event that UI layer can subscribe to for questions
        public static event Func<string, string, NotificationType, NotificationResult>? OnQuestion;

        // Event that UI layer can subscribe to for input
        public static event Func<string, string, string, string?>? OnInputBox;

        public static NotificationResult ShowInfo(string message, string title = "Information")
        {
            OnNotification?.Invoke(title, message, NotificationType.Info);
            return NotificationResult.OK;
        }

        public static NotificationResult ShowWarning(string message, string title = "Warning")
        {
            OnNotification?.Invoke(title, message, NotificationType.Warning);
            return NotificationResult.OK;
        }

        public static NotificationResult ShowError(string message, string title = "Error")
        {
            OnNotification?.Invoke(title, message, NotificationType.Error);
            return NotificationResult.OK;
        }

        public static NotificationResult ShowQuestion(string message, string title = "Question")
        {
            return OnQuestion?.Invoke(title, message, NotificationType.Question) ?? NotificationResult.No;
        }

        public static string? InputBox(string prompt, string title = "Input", string defaultValue = "")
        {
            return OnInputBox?.Invoke(title, prompt, defaultValue);
        }
    }
}
