using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class PingLogService
    {
        private readonly SettingsService _settingsService = new SettingsService();
        private const string DefaultLogFolderName = "PingLogs";

        public string GetLogFolderPath()
        {
            var configuredPath = _settingsService.GetPingLogFolderPath();
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            var appDataRoot = Path.GetDirectoryName(ConfigFileHelper.GetConfigIniPath()) ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDataRoot, DefaultLogFolderName);
        }

        public string GetLogFilePath(string target)
        {
            var safeTarget = BuildSafeTargetName(target);
            var targetHash = ComputeStableHash(target);
            var fileName = $"{safeTarget}_{targetHash}.log";
            return Path.Combine(GetLogFolderPath(), fileName);
        }

        public void AppendPingResult(string target, string protocol, string response)
        {
            try
            {
                var filePath = GetLogFilePath(target);
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                Directory.CreateDirectory(directory);

                if (!File.Exists(filePath))
                {
                    File.AppendAllLines(filePath, new[] { "Zeit;Ziel;Protokoll;Antwortzeit" });
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var line = $"{timestamp};{target};{protocol};{response}";
                File.AppendAllLines(filePath, new[] { line });
            }
            catch
            {
                // Logging darf den Ping-Loop nicht unterbrechen.
            }
        }

        public void OpenLogFile(string target)
        {
            var filePath = GetLogFilePath(target);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath))
            {
                File.AppendAllLines(filePath, new[] { "Zeit;Ziel;Protokoll;Antwortzeit" });
            }

            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };

            Process.Start(psi);
        }

        public bool LogFileExists(string target)
        {
            var filePath = GetLogFilePath(target);
            return File.Exists(filePath);
        }

        public bool TryDeleteLogFile(string target)
        {
            try
            {
                var filePath = GetLogFilePath(target);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryExportAndDeleteLogFile(string target, string destinationPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return false;
                }

                var sourcePath = GetLogFilePath(target);
                if (!File.Exists(sourcePath))
                {
                    return false;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourcePath, destinationPath, true);
                File.Delete(sourcePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildSafeTargetName(string target)
        {
            var source = string.IsNullOrWhiteSpace(target) ? "target" : target.Trim();
            var invalidChars = Path.GetInvalidFileNameChars();
            var normalized = new string(source.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(normalized) ? "target" : normalized;
        }

        private static string ComputeStableHash(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return Convert.ToHexString(bytes.AsSpan(0, 4));
        }
    }
}
