using System;
using System.IO;
using System.Runtime.InteropServices;

namespace neTiPx.Helpers
{
    public enum LogLevel
    {
        INFO,
        WARN,
        ERROR
    }

    internal static class DebugLogger
    {
        private static readonly string _logPath = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
            "netipx", "debug.log");

        private static readonly object _lock = new object();
        private static bool _headerWritten = false;

        // Hauptmethode – strukturierter Eintrag mit Level und Bereich
        public static void Log(LogLevel level, string area, string message)
        {
            Write($"[{LevelLabel(level)}] | {area,-16} | {message}");
        }

        // Overload mit Exception
        public static void Log(LogLevel level, string area, string message, Exception ex)
        {
            Log(level, area, $"{message} | {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                Log(level, area, $"{message} | InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            if (ex.HResult != 0)
                Log(level, area, $"{message} | HResult: 0x{ex.HResult:X8}");
        }

        private static string LevelLabel(LogLevel level) => level switch
        {
            LogLevel.WARN  => "WARN ",
            LogLevel.ERROR => "ERROR",
            _              => "INFO "
        };

        private static void Write(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(_logPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                lock (_lock)
                {
                    if (!_headerWritten)
                    {
                        _headerWritten = true;
                        var header =
                            $"=== neTiPx debug log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}" +
                            $"OS       : {RuntimeInformation.OSDescription}{Environment.NewLine}" +
                            $"Arch     : {RuntimeInformation.OSArchitecture} / Process: {RuntimeInformation.ProcessArchitecture}{Environment.NewLine}" +
                            $"Runtime  : {RuntimeInformation.FrameworkDescription}{Environment.NewLine}" +
                            $"User     : {Environment.UserName} / Domain: {Environment.UserDomainName}{Environment.NewLine}" +
                            $"Is64Proc : {Environment.Is64BitProcess} / Is64OS: {Environment.Is64BitOperatingSystem}{Environment.NewLine}" +
                            $"LogFile  : {_logPath}{Environment.NewLine}" +
                            new string('-', 70) + Environment.NewLine +
                            $"{"Zeitstempel",-23} | {"Level",-7} | {"Bereich",-16} | Nachricht{Environment.NewLine}" +
                            new string('-', 70) + Environment.NewLine;
                        File.AppendAllText(_logPath, header, System.Text.Encoding.UTF8);
                    }

                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}";
                    File.AppendAllText(_logPath, line, System.Text.Encoding.UTF8);
                }
            }
            catch
            {
                // Logging darf die App niemals crashen
            }
        }
    }
}
