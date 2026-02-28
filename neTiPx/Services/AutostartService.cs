using Microsoft.Win32;
using System;
using System.IO;
using System.Diagnostics;

namespace neTiPx.Services
{
    public sealed class AutostartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "neTiPx";

        public bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    var exePath = GetExecutablePath();
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        key.SetValue(ValueName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch
            {
            }
        }

        private static string GetExecutablePath()
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return processPath;
            }

            try
            {
                var modulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(modulePath))
                {
                    return modulePath;
                }
            }
            catch
            {
            }

            return Path.Combine(AppContext.BaseDirectory, "neTiPx.exe");
        }
    }
}
