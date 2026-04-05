using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Helpers;
using neTiPx.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;

namespace neTiPx.Views
{
    public sealed partial class LogViewerHighlightConfigDialog : UserControl
    {
        public ObservableCollection<LogViewerHighlightRule> Rules { get; } = new ObservableCollection<LogViewerHighlightRule>();

        public IReadOnlyList<HighlightColorOptionItem> ColorOptions { get; }

        public LogViewerHighlightConfigDialog(IEnumerable<LogViewerHighlightRule> rules, IEnumerable<HighlightColorOptionItem> colorOptions)
        {
            InitializeComponent();
            ColorOptions = colorOptions.ToList();
            RulesListView.Tag = ColorOptions;

            foreach (var rule in rules)
            {
                Rules.Add(new LogViewerHighlightRule
                {
                    SearchText = rule.SearchText,
                    ColorKey = rule.ColorKey
                });
            }
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
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "LogViewerHighlights"
            };
            picker.FileTypeChoices.Clear();
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });

            var hwnd = WindowHelper.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var saveFile = await picker.PickSaveFileAsync();
            if (saveFile == null)
            {
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
            await FileIO.WriteTextAsync(saveFile, json);
        }

        private async void ImportRulesButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");

            var hwnd = WindowHelper.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            try
            {
                var json = await FileIO.ReadTextAsync(file);
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
            }
            catch
            {
            }
        }
    }

    public sealed class HighlightColorOptionItem
    {
        public HighlightColorOptionItem(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public string Key { get; }

        public string DisplayName { get; }
    }

    public sealed class HighlightRuleExport
    {
        public string SearchText { get; set; } = string.Empty;

        public string ColorKey { get; set; } = "red";
    }
}
