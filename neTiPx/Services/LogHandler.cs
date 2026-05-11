using System;
using System.Collections.Generic;
using System.Text;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public static class LogHandler
    {
        public const string CategoryUserEvent = "USER_EVENT";
        public const string CategorySystemEvent = "SYSTEM_EVENT";
        public const string CategoryErrorEvent = "ERROR_EVENT";

        public static void Log(LogLevel level, string area, string message)
        {
            DebugLogger.Log(level, area, message);
        }

        public static void Log(LogLevel level, string area, string message, Exception ex)
        {
            DebugLogger.Log(level, area, message, ex);
        }

        public static void LogEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            LogUserEvent(area, eventName, detail, payload);
        }

        public static void LogUserEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            Log(LogLevel.INFO, area, BuildCategorizedMessage(CategoryUserEvent, eventName, detail, payload));
        }

        public static void LogSystemEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            Log(LogLevel.INFO, area, BuildCategorizedMessage(CategorySystemEvent, eventName, detail, payload));
        }

        public static void LogErrorEvent(string area, string eventName, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, eventName, detail, payload));
        }

        public static void LogErrorEvent(string area, string eventName, Exception ex, string? detail = null, IReadOnlyDictionary<string, string?>? payload = null)
        {
            Log(LogLevel.ERROR, area, BuildCategorizedMessage(CategoryErrorEvent, eventName, detail, payload), ex);
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

            LogEvent(area, "SettingChanged", $"{settingName}: '{oldValue ?? string.Empty}' -> '{newValue ?? string.Empty}'");
        }
    }
}
