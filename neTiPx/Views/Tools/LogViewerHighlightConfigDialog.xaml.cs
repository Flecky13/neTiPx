using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
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
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = T("LOGVIEWER_HIGHLIGHT_EXPORT_FILENAME")
            };
            picker.FileTypeChoices.Clear();
            picker.FileTypeChoices.Add(T("LOGVIEWER_FILETYPE_JSON"), new List<string> { ".json" });

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
