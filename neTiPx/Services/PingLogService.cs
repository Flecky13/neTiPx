using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class PingLogService
    {
        private readonly SettingsService _settingsService = new SettingsService();
        private const string DefaultLogFolderName = "PingLogs";
        private const string LegacyLogHeader = "Zeit;Ziel;Protokoll;Antwortzeit";
        private const string PreviousLogHeader = "Zeit;Ziel;Protokoll;Antwortzeit;ResolvedIP";
        private const string LogHeader = "Protokoll: Zeit;DN;IP;Antwortzeit";

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

        public void AppendPingResult(string target, string protocol, string response, string resolvedIp = "")
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
                EnsureLogHeader(filePath);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var domainName = GetDomainNameForLog(target);
                var ipForLog = GetIpForLog(target, resolvedIp);
                var line = $"{protocol}: {timestamp};{domainName};{ipForLog};{response}";
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

            EnsureLogHeader(filePath);

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

        private static void EnsureLogHeader(string filePath)
        {
            if (!File.Exists(filePath))
            {
                File.AppendAllLines(filePath, new[] { LogHeader });
                return;
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
            {
                File.AppendAllLines(filePath, new[] { LogHeader });
                return;
            }

            if (string.Equals(lines[0], LegacyLogHeader, StringComparison.OrdinalIgnoreCase)
                || string.Equals(lines[0], PreviousLogHeader, StringComparison.OrdinalIgnoreCase))
            {
                lines[0] = LogHeader;
                File.WriteAllLines(filePath, lines);
            }
        }

        private static string GetDomainNameForLog(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return "nicht bekannt";
            }

            return IPAddress.TryParse(target, out _)
                ? "nicht bekannt"
                : target;
        }

        private static string GetIpForLog(string target, string resolvedIp)
        {
            if (!string.IsNullOrWhiteSpace(resolvedIp))
            {
                return resolvedIp;
            }

            return IPAddress.TryParse(target, out _) ? target : "nicht bekannt";
        }
    }
}
