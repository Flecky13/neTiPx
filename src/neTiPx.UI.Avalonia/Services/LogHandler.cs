using System;
using System.Collections.Generic;
using System.Text;
using neTiPx.UI.Avalonia.Helpers;

namespace neTiPx.UI.Avalonia.Services
{
    public static class LogHandler
    {
        public const string CategoryUserEvent = "USER_EVENT";
        public const string CategorySystemEvent = "SYSTEM_EVENT";
        public const string CategoryErrorEvent = "ERROR_EVENT";

        public const string EventMessage = "Message";
        public const string EventException = "Exception";
        public const string EventError = "Error";

        public static void LogUserEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            DebugLogger.Log(LogLevel.INFO, area, BuildCategorizedMessage(CategoryUserEvent, eventName, detail, payload));
        }

        public static void LogSystemEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            DebugLogger.Log(LogLevel.INFO, area, BuildCategorizedMessage(CategorySystemEvent, eventName, detail, payload));
        }

        public static void LogSystemEvent(LogLevel level, string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            DebugLogger.Log(level, area, BuildCategorizedMessage(CategorySystemEvent, eventName, detail, payload));
        }

        public static void LogSystemMessage(LogLevel level, string area, string message)
        {
            var category = level == LogLevel.ERROR ? CategoryErrorEvent : CategorySystemEvent;
            var eventName = level == LogLevel.ERROR ? EventError : EventMessage;
            DebugLogger.Log(level, area, BuildCategorizedMessage(category, eventName, message, null));
        }

        public static void LogSystemMessage(LogLevel level, string area, string message, Exception ex)
        {
            var category = level == LogLevel.ERROR ? CategoryErrorEvent : CategorySystemEvent;
            DebugLogger.Log(level, area, BuildCategorizedMessage(category, EventException, message, null), ex);
        }

        public static void LogErrorEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            DebugLogger.Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, eventName, detail, payload));
        }

        public static void LogErrorEvent(string area, string eventName, Exception ex, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            DebugLogger.Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, eventName, detail, payload), ex);
        }

        public static void LogErrorMessage(string area, string message)
        {
            DebugLogger.Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, EventError, message, null));
        }

        public static void LogErrorMessage(string area, string message, Exception ex)
        {
            DebugLogger.Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, EventException, message, null), ex);
        }

        private static string BuildCategorizedMessage(string category, string? eventName, string? detail, IReadOnlyDictionary<string, string?>? payload)
        {
            var message = new StringBuilder();
            message.Append(category);

            if (!string.IsNullOrWhiteSpace(eventName))
            {
                message.Append(" | ").Append(eventName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                message.Append(" | ").Append(detail.Trim());
            }

            if (payload != null)
            {
                foreach (var pair in payload)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    message.Append(" | ").Append(pair.Key.Trim()).Append("=");
                    message.Append(pair.Value?.Trim() ?? string.Empty);
                }
            }

            return message.ToString();
        }

        public static void LogSettingChange(string area, string settingName, string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            LogUserEvent(area, "SettingChanged", $"{settingName}: '{oldValue ?? string.Empty}' -> '{newValue ?? string.Empty}'");
        }
    }
}
