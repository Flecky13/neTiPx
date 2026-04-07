using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace neTiPx.Views
{
    public sealed partial class LogViewerHighlightConfigDialog : UserControl
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;

        public ObservableCollection<LogViewerHighlightRule> Rules { get; } = new ObservableCollection<LogViewerHighlightRule>();

        public IReadOnlyList<HighlightColorOptionItem> ColorOptions { get; }

        public LogViewerHighlightConfigDialog(IEnumerable<LogViewerHighlightRule> rules, IEnumerable<HighlightColorOptionItem> colorOptions)
        {
            InitializeComponent();
            ColorOptions = colorOptions.ToList();
            RulesListView.Tag = ColorOptions;
            UpdateLanguage();

            foreach (var rule in rules)
            {
                Rules.Add(new LogViewerHighlightRule
                {
                    SearchText = rule.SearchText,
                    ColorKey = rule.ColorKey
                });
            }
        }

        private static string T(string key) => _lm.Lang(key);

        private void UpdateLanguage()
        {
            if (AddRuleButton != null) AddRuleButton.Content = T("LOGVIEWER_HIGHLIGHT_ADD_RULE");
            if (ImportRulesButton != null) ImportRulesButton.Content = T("LOGVIEWER_HIGHLIGHT_IMPORT");
            if (ExportRulesButton != null) ExportRulesButton.Content = T("LOGVIEWER_HIGHLIGHT_EXPORT");
            if (DialogRoot != null) DialogRoot.Tag = T("LOGVIEWER_HIGHLIGHT_SEARCH_PLACEHOLDER");
        }

        public IReadOnlyList<LogViewerHighlightRule> GetRules()
        {
            return Rules
                .Where(rule => !string.IsNullOrWhiteSpace(rule.SearchText))
                .Select(rule => new LogViewerHighlightRule
                {
                    SearchText = rule.SearchText.Trim(),
                    ColorKey = rule.ColorKey
                })
                .ToList();
        }

        private void AddRuleButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultColorKey = ColorOptions.FirstOrDefault()?.Key ?? "red";
            Rules.Add(new LogViewerHighlightRule
            {
                SearchText = string.Empty,
                ColorKey = defaultColorKey
            });
        }

        private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is LogViewerHighlightRule rule)
            {
                Rules.Remove(rule);
            }
        }

        private async void ExportRulesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DebugLogger.Log(LogLevel.INFO, "LogViewer", "Highlight-Regeln exportieren: Dialog öffnen");

                var hwnd = App.MainWindow != null
                    ? WindowHelper.GetWindowHandle(App.MainWindow)
                    : IntPtr.Zero;
                DebugLogger.Log(LogLevel.INFO, "LogViewer", $"Export HWND={hwnd}");
                if (hwnd == IntPtr.Zero)
                {
                    DebugLogger.Log(LogLevel.ERROR, "LogViewer", "Export fehlgeschlagen | Kein gueltiges Owner-HWND gefunden");
                    return;
                }

                var filter = FileDialogHelper.BuildFilter(("JSON-Dateien (*.json)", "*.json"));
                var suggestedFileName = T("LOGVIEWER_HIGHLIGHT_EXPORT_FILENAME") + ".json";
                var selected = FileDialogHelper.TrySaveFile(hwnd, T("LOGVIEWER_HIGHLIGHT_EXPORT"), filter, "json", suggestedFileName, out var savePath);
                if (!selected)
                {
                    DebugLogger.Log(LogLevel.INFO, "LogViewer", "Export abgebrochen");
                    return;
                }

                var payload = GetRules()
                    .Select(rule => new HighlightRuleExport
                    {
                        SearchText = rule.SearchText,
                        ColorKey = rule.ColorKey
                    })
                    .ToList();

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(savePath, json);
                DebugLogger.Log(LogLevel.INFO, "LogViewer", $"Highlight-Regeln exportiert nach: {savePath} ({payload.Count} Regel(n))");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogLevel.ERROR, "LogViewer", "Export fehlgeschlagen", ex);
            }
        }

        private async void ImportRulesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DebugLogger.Log(LogLevel.INFO, "LogViewer", "Highlight-Regeln importieren: Dialog öffnen");

                var hwnd = App.MainWindow != null
                    ? WindowHelper.GetWindowHandle(App.MainWindow)
                    : IntPtr.Zero;
                DebugLogger.Log(LogLevel.INFO, "LogViewer", $"Import HWND={hwnd}");
                if (hwnd == IntPtr.Zero)
                {
                    DebugLogger.Log(LogLevel.ERROR, "LogViewer", "Import fehlgeschlagen | Kein gueltiges Owner-HWND gefunden");
                    return;
                }

                var filter = FileDialogHelper.BuildFilter(("JSON-Dateien (*.json)", "*.json"));
                var selected = FileDialogHelper.TryOpenFile(hwnd, T("LOGVIEWER_HIGHLIGHT_IMPORT"), filter, out var importPath);
                if (!selected)
                {
                    DebugLogger.Log(LogLevel.INFO, "LogViewer", "Import abgebrochen");
                    return;
                }

                DebugLogger.Log(LogLevel.INFO, "LogViewer", $"Import Datei: {importPath}");

                try
                {
                    var json = await File.ReadAllTextAsync(importPath);
                    var payload = JsonSerializer.Deserialize<List<HighlightRuleExport>>(json) ?? new List<HighlightRuleExport>();

                    Rules.Clear();
                    foreach (var rule in payload.Where(rule => !string.IsNullOrWhiteSpace(rule.SearchText)))
                    {
                        Rules.Add(new LogViewerHighlightRule
                        {
                            SearchText = rule.SearchText.Trim(),
                            ColorKey = string.IsNullOrWhiteSpace(rule.ColorKey) ? "red" : rule.ColorKey
                        });
                    }
                    DebugLogger.Log(LogLevel.INFO, "LogViewer", $"Highlight-Regeln importiert: {Rules.Count} Regel(n)");
                }
                catch (Exception parseEx)
                {
                    DebugLogger.Log(LogLevel.ERROR, "LogViewer", "Import JSON-Parsing fehlgeschlagen", parseEx);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogLevel.ERROR, "LogViewer", "Import fehlgeschlagen", ex);
            }
        }
    }

    public sealed class HighlightColorOptionItem
    {
        public HighlightColorOptionItem(string key, Brush swatchBrush)
        {
            Key = key;
            SwatchBrush = swatchBrush;
        }

        public string Key { get; }

        public Brush SwatchBrush { get; }
    }

    public sealed class HighlightRuleExport
    {
        public string SearchText { get; set; } = string.Empty;

        public string ColorKey { get; set; } = "red";
    }
}
