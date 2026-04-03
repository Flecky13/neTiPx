using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class SettingsPageWarmupService
    {
        private readonly object _syncRoot = new();
        private Task<SettingsPageWarmupSnapshot>? _preloadTask;

        public void StartPreload()
        {
            lock (_syncRoot)
            {
                if (_preloadTask == null)
                {
                    Debug.WriteLine("[SettingsWarmup][Service] StartPreload scheduled");
                    _preloadTask = Task.Run(LoadSnapshot);
                }
            }
        }

        public SettingsPageWarmupSnapshot PreloadNow()
        {
            Debug.WriteLine("[SettingsWarmup][Service] PreloadNow requested");
            StartPreload();
            var stopwatch = Stopwatch.StartNew();
            var snapshot = _preloadTask!.GetAwaiter().GetResult();
            stopwatch.Stop();
            Debug.WriteLine($"[SettingsWarmup][Service] PreloadNow returned after {stopwatch.ElapsedMilliseconds} ms");
            return snapshot;
        }

        public bool TryGetSnapshot(out SettingsPageWarmupSnapshot? snapshot)
        {
            StartPreload();

            if (_preloadTask?.IsCompletedSuccessfully == true)
            {
                Debug.WriteLine("[SettingsWarmup][Service] TryGetSnapshot hit completed snapshot");
                snapshot = _preloadTask.Result;
                return true;
            }

            Debug.WriteLine("[SettingsWarmup][Service] TryGetSnapshot miss - snapshot not ready yet");
            snapshot = null;
            return false;
        }

        public Task<SettingsPageWarmupSnapshot> GetSnapshotAsync()
        {
            StartPreload();
            Debug.WriteLine("[SettingsWarmup][Service] GetSnapshotAsync awaiting preload task");
            return _preloadTask!;
        }

        private static SettingsPageWarmupSnapshot LoadSnapshot()
        {
            var stopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[SettingsWarmup][Service] LoadSnapshot start T:{Environment.CurrentManagedThreadId}");

            var settingsService = new SettingsService();
            var themeService = new ThemeSettingsService();
            var adapterStore = new AdapterStore();
            var autostartService = new AutostartService();

            var userSettings = settingsService.GetUserSettings();
            var themes = themeService.LoadThemes();
            var adapterSettings = adapterStore.ReadAdapters();
            var adapterNames = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.GetPhysicalAddress() != null && n.GetPhysicalAddress().GetAddressBytes().Length > 0)
                .Select(n => n.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var pingLogFolderPath = !string.IsNullOrWhiteSpace(userSettings.PingLogFolderPath)
                ? userSettings.PingLogFolderPath
                : Path.Combine(Path.GetDirectoryName(ConfigFileHelper.GetConfigIniPath()) ?? AppDomain.CurrentDomain.BaseDirectory, "PingLogs");

            var snapshot = new SettingsPageWarmupSnapshot(
                userSettings,
                themes,
                adapterSettings,
                adapterNames,
                pingLogFolderPath,
                autostartService.IsEnabled());

            stopwatch.Stop();
            Debug.WriteLine($"[SettingsWarmup][Service] LoadSnapshot done after {stopwatch.ElapsedMilliseconds} ms; themes={themes.Count}; adapters={adapterNames.Count}");
            return snapshot;
        }
    }

    public sealed class SettingsPageWarmupSnapshot
    {
        public SettingsPageWarmupSnapshot(
            UserSettingsStore.UserSettings userSettings,
            List<ColorTheme> themes,
            AdapterStore.AdapterSettings adapterSettings,
            List<string> adapterNames,
            string pingLogFolderPath,
            bool autostartEnabled)
        {
            UserSettings = userSettings;
            Themes = themes;
            AdapterSettings = adapterSettings;
            AdapterNames = adapterNames;
            PingLogFolderPath = pingLogFolderPath;
            AutostartEnabled = autostartEnabled;
        }

        public UserSettingsStore.UserSettings UserSettings { get; }
        public List<ColorTheme> Themes { get; }
        public AdapterStore.AdapterSettings AdapterSettings { get; }
        public List<string> AdapterNames { get; }
        public string PingLogFolderPath { get; }
        public bool AutostartEnabled { get; }
    }
}
