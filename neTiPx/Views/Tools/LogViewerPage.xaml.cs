using Microsoft.UI;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace neTiPx.Views
{
    public sealed partial class LogViewerPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly SolidColorBrush _defaultBackgroundBrush = new SolidColorBrush(Colors.Transparent);
        private readonly SolidColorBrush _defaultForegroundBrush = new SolidColorBrush(Colors.White);
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private readonly List<LogViewerLine> _allLines = new List<LogViewerLine>();
        private readonly LogViewerStore _logViewerStore = new LogViewerStore();
        private readonly LogViewerHighlightStore _highlightStore = new LogViewerHighlightStore();
        private FileSystemWatcher? _fileWatcher;
        private readonly Timer _reloadDebounceTimer;
        private string _currentFilePath = string.Empty;
        private bool _suppressFileSelectionChange;
        private bool _autoScrollEnabled = true;
        private long _lastKnownFileLength;
        private DateTime _lastKnownFileWriteTimeUtc = DateTime.MinValue;
        private bool _lastLoadEndedWithLineBreak;
        private ScrollViewer? _logListScrollViewer;
        private bool _isPinnedToBottom = true;
        private DateTimeOffset _lastLiveReloadAt = DateTimeOffset.MinValue;

        public ObservableCollection<LogViewerLine> Lines { get; } = new ObservableCollection<LogViewerLine>();
        public ObservableCollection<RecentFileEntry> RecentFiles { get; } = new ObservableCollection<RecentFileEntry>();
        public ObservableCollection<LogViewerHighlightRule> HighlightRules { get; } = new ObservableCollection<LogViewerHighlightRule>();
        public ObservableCollection<HighlightColorOptionItem> HighlightColorOptions { get; } = new ObservableCollection<HighlightColorOptionItem>();

        public LogViewerPage()
        {
            InitializeComponent();
            _reloadDebounceTimer = new Timer(OnReloadDebounceTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            InitializeHighlightColors();
            Loaded += LogViewerPage_Loaded;
            Unloaded += LogViewerPage_Unloaded;
        }

        private static string T(string key) => _lm.Lang(key);

        private void LogViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;
            EnsureLogListScrollViewer();
            LoadHighlightRules();
            LoadRecentFiles();
            UpdateLanguage();
            UpdateMatchInfo();
        }

        private void LogViewerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            DetachLogListScrollViewer();
            DisposeWatcher();
            _reloadDebounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
            UpdateMatchInfo();
        }

        private void UpdateLanguage()
        {
            if (LogViewerTitleText != null) LogViewerTitleText.Text = T("TOOLS_LOG_VIEWER");
            if (LogViewerOpenButton != null) LogViewerOpenButton.Content = T("LOGVIEWER_BUTTON_OPEN");
            if (LogViewerHighlightConfigButton != null) LogViewerHighlightConfigButton.Content = T("LOGVIEWER_HIGHLIGHT_CONFIG_BUTTON");
            if (LogViewerAutoRefreshCheckBox != null) LogViewerAutoRefreshCheckBox.Content = T("LOGVIEWER_AUTO_REFRESH");
            if (LogViewerFilterTextBox != null)
            {
                LogViewerFilterTextBox.Header = T("LOGVIEWER_FILTER_HEADER");
                LogViewerFilterTextBox.PlaceholderText = T("LOGVIEWER_FILTER_PLACEHOLDER");
            }

            if (LogViewerFileComboBox != null && string.IsNullOrWhiteSpace(_currentFilePath))
            {
                LogViewerFileComboBox.PlaceholderText = T("LOGVIEWER_NO_FILE");
            }

            if (LogViewerReloadButton != null) ToolTipService.SetToolTip(LogViewerReloadButton, T("LOGVIEWER_BUTTON_RELOAD"));
            if (LogViewerAutoRefreshCheckBox != null) ToolTipService.SetToolTip(LogViewerAutoRefreshCheckBox, T("LOGVIEWER_AUTO_REFRESH"));
            if (LogViewerHighlightConfigButton != null) ToolTipService.SetToolTip(LogViewerHighlightConfigButton, T("LOGVIEWER_HIGHLIGHT_CONFIG_BUTTON"));

            InitializeHighlightColors();
        }

        private async void LogViewerOpenButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".log");
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add("*");

            var hwnd = WindowHelper.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            AddRecentFile(file.Path);
            await LoadFileAsync(file.Path, forceStatusMessage: true);
        }

        private async void LogViewerFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFileSelectionChange || LogViewerFileComboBox?.SelectedItem is not RecentFileEntry selectedEntry)
            {
                return;
            }

            if (string.Equals(selectedEntry.FullPath, _currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AddRecentFile(selectedEntry.FullPath);
            await LoadFileAsync(selectedEntry.FullPath, forceStatusMessage: true);
        }

        private void LogViewerFileComboBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshRecentFileDisplayTexts();
        }

        private async void LogViewerReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                return;
            }

            await LoadFileAsync(_currentFilePath, forceStatusMessage: true);
        }

        private void LogViewerAutoRefreshCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _autoScrollEnabled = LogViewerAutoRefreshCheckBox?.IsChecked == true;
            if (_autoScrollEnabled)
            {
                _isPinnedToBottom = true;
                ScrollToLatestLine(force: true);
            }
        }

        private void LogViewerFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshVisibleLines();
        }

        private async void LogViewerHighlightConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dialogContent = new LogViewerHighlightConfigDialog(HighlightRules, HighlightColorOptions);
            var dialog = new ContentDialog
            {
                Title = T("LOGVIEWER_HIGHLIGHT_CONFIG_TITLE"),
                Content = dialogContent,
                PrimaryButtonText = T("LOGVIEWER_HIGHLIGHT_CONFIG_SAVE"),
                CloseButtonText = T("ROUTES_DIALOG_CANCEL"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            HighlightRules.Clear();
            foreach (var rule in dialogContent.GetRules())
            {
                HighlightRules.Add(rule);
            }

            _highlightStore.WriteRules(HighlightRules);
            RefreshVisibleLines();
        }

        private async Task LoadFileAsync(string filePath, bool forceStatusMessage, bool scrollToEnd = false)
        {
            await _loadLock.WaitAsync();
            try
            {
                SetLoadingState(true);
                var fileSnapshot = await ReadAllLinesWithRetryAsync(filePath);

                _currentFilePath = filePath;
                _lastKnownFileLength = fileSnapshot.FileLength;
                _lastKnownFileWriteTimeUtc = fileSnapshot.LastWriteTimeUtc;
                _lastLoadEndedWithLineBreak = fileSnapshot.EndsWithLineBreak;
                SelectRecentFile(filePath);
                ToolTipService.SetToolTip(LogViewerFileComboBox, filePath);

                _allLines.Clear();
                Lines.Clear();
                for (int i = 0; i < fileSnapshot.Lines.Count; i++)
                {
                    var line = new LogViewerLine
                    {
                        LineNumber = i + 1,
                        Text = fileSnapshot.Lines[i],
                        BackgroundBrush = _defaultBackgroundBrush,
                        ForegroundBrush = _defaultForegroundBrush
                    };
                    _allLines.Add(line);
                }

                EnsureWatcher(filePath);
                RefreshVisibleLines();

                if (scrollToEnd)
                {
                    ScrollToLatestLine(force: true);
                }
                else if (_autoScrollEnabled && _isPinnedToBottom)
                {
                    ScrollToLatestLine(force: true);
                }

            }
            catch (Exception ex)
            {
            }
            finally
            {
                SetLoadingState(false);
                _loadLock.Release();
            }
        }

        private async Task AppendLatestChangesAsync(string filePath)
        {
            var requiresFullReload = false;

            await _loadLock.WaitAsync();
            try
            {
                SetLoadingState(true);

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    requiresFullReload = true;
                }
                else
                {
                    var currentLength = fileInfo.Length;
                    var currentWriteTimeUtc = fileInfo.LastWriteTimeUtc;
                    if (currentLength < _lastKnownFileLength)
                    {
                        requiresFullReload = true;
                    }
                    else if (currentLength == _lastKnownFileLength && currentWriteTimeUtc <= _lastKnownFileWriteTimeUtc)
                    {
                        return;
                    }
                    else
                    {
                        var appendedContent = await ReadFileRangeWithRetryAsync(filePath, _lastKnownFileLength);
                        if (string.IsNullOrEmpty(appendedContent))
                        {
                            _lastKnownFileLength = currentLength;
                            return;
                        }

                        var parsedChunk = ParseLogContent(appendedContent);
                        var changedLineIndices = AppendParsedChunk(parsedChunk);

                        _lastKnownFileLength = currentLength;
                        _lastKnownFileWriteTimeUtc = currentWriteTimeUtc;
                        _lastLoadEndedWithLineBreak = parsedChunk.EndsWithLineBreak;

                        ApplyIncrementalVisibleUpdate(changedLineIndices);
                        ScrollToLatestLine();
                    }
                }
            }
            finally
            {
                SetLoadingState(false);
                _loadLock.Release();
            }

            if (requiresFullReload)
            {
                await LoadFileAsync(filePath, forceStatusMessage: false, scrollToEnd: true);
            }
        }

        private async Task<LogFileSnapshot> ReadAllLinesWithRetryAsync(string filePath)
        {
            Exception? lastError = null;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    var content = await reader.ReadToEndAsync();
                    var parsedContent = ParseLogContent(content);
                    var lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
                    return new LogFileSnapshot(parsedContent.Lines, stream.Length, lastWriteTimeUtc, parsedContent.EndsWithLineBreak);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await Task.Delay(120);
                }
            }

            throw lastError ?? new IOException(T("LOGVIEWER_STATUS_ERROR"));
        }

        private async Task<string> ReadFileRangeWithRetryAsync(string filePath, long startOffset)
        {
            Exception? lastError = null;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    stream.Seek(startOffset, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                    return await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await Task.Delay(120);
                }
            }

            throw lastError ?? new IOException(T("LOGVIEWER_STATUS_ERROR"));
        }

        private static ParsedLogContent ParseLogContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return new ParsedLogContent(Array.Empty<string>(), false);
            }

            var normalizedContent = content.Replace("\r\n", "\n").Replace('\r', '\n');
            var endsWithLineBreak = normalizedContent.EndsWith('\n');
            var lines = normalizedContent.Split('\n').ToList();

            if (endsWithLineBreak && lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return new ParsedLogContent(lines, endsWithLineBreak);
        }

        private List<int> AppendParsedChunk(ParsedLogContent parsedChunk)
        {
            var changedLineIndices = new List<int>();
            if (parsedChunk.Lines.Count == 0)
            {
                return changedLineIndices;
            }

            var appendStartIndex = 0;

            if (!_lastLoadEndedWithLineBreak && _allLines.Count > 0)
            {
                var lastLine = _allLines[^1];
                lastLine.Text += parsedChunk.Lines[0];
                changedLineIndices.Add(_allLines.Count - 1);
                appendStartIndex = 1;
            }

            for (int i = appendStartIndex; i < parsedChunk.Lines.Count; i++)
            {
                _allLines.Add(new LogViewerLine
                {
                    LineNumber = _allLines.Count + 1,
                    Text = parsedChunk.Lines[i]
                });
                changedLineIndices.Add(_allLines.Count - 1);
            }

            return changedLineIndices;
        }

        private void BuildSegmentsForLine(LogViewerLine line, int lineIndex)
        {
            var matches = GetLineMatches(line.Text, lineIndex);
            line.IsMatch = matches.Count > 0;
            line.IsActiveMatch = false;
            line.BackgroundBrush = _defaultBackgroundBrush;
            line.ForegroundBrush = _defaultForegroundBrush;
            line.Segments = new ObservableCollection<LogViewerTextSegment>(CreateSegments(line.Text, matches));
        }

        private List<HighlightMatch> GetLineMatches(string text, int lineIndex)
        {
            var lineMatches = new List<HighlightMatch>();
            if (string.IsNullOrEmpty(text))
            {
                return lineMatches;
            }

            var occupied = new bool[text.Length];

            for (int ruleIndex = 0; ruleIndex < HighlightRules.Count; ruleIndex++)
            {
                var rule = HighlightRules[ruleIndex];
                if (string.IsNullOrWhiteSpace(rule.SearchText))
                {
                    continue;
                }

                var searchText = rule.SearchText.Trim();
                var searchStart = 0;
                while (searchStart < text.Length)
                {
                    var matchIndex = text.IndexOf(searchText, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    var overlaps = false;
                    for (int i = 0; i < searchText.Length; i++)
                    {
                        if (matchIndex + i < occupied.Length && occupied[matchIndex + i])
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        for (int i = 0; i < searchText.Length && matchIndex + i < occupied.Length; i++)
                        {
                            occupied[matchIndex + i] = true;
                        }

                        var match = new HighlightMatch(lineIndex, matchIndex, searchText.Length, ruleIndex);
                        lineMatches.Add(match);
                    }

                    searchStart = matchIndex + Math.Max(1, searchText.Length);
                }
            }

            return lineMatches.OrderBy(match => match.Start).ToList();
        }

        private IReadOnlyList<LogViewerTextSegment> CreateSegments(string text, IReadOnlyList<HighlightMatch> matches)
        {
            var segments = new List<LogViewerTextSegment>();
            if (matches.Count == 0)
            {
                segments.Add(new LogViewerTextSegment
                {
                    Text = text,
                    BackgroundBrush = _defaultBackgroundBrush,
                    ForegroundBrush = _defaultForegroundBrush
                });
                return segments;
            }

            var position = 0;
            foreach (var match in matches)
            {
                if (match.Start > position)
                {
                    segments.Add(new LogViewerTextSegment
                    {
                        Text = text.Substring(position, match.Start - position),
                        BackgroundBrush = _defaultBackgroundBrush,
                        ForegroundBrush = _defaultForegroundBrush
                    });
                }

                var rule = match.RuleIndex >= 0 && match.RuleIndex < HighlightRules.Count
                    ? HighlightRules[match.RuleIndex]
                    : null;
                var colors = GetRuleBrushes(rule?.ColorKey ?? "red");

                segments.Add(new LogViewerTextSegment
                {
                    Text = text.Substring(match.Start, match.Length),
                    BackgroundBrush = colors.BackgroundBrush,
                    ForegroundBrush = colors.ForegroundBrush
                });

                position = match.Start + match.Length;
            }

            if (position < text.Length)
            {
                segments.Add(new LogViewerTextSegment
                {
                    Text = text.Substring(position),
                    BackgroundBrush = _defaultBackgroundBrush,
                    ForegroundBrush = _defaultForegroundBrush
                });
            }

            return segments;
        }

        private void RefreshVisibleLines()
        {
            var filterText = LogViewerFilterTextBox?.Text?.Trim() ?? string.Empty;
            var filteredLines = string.IsNullOrWhiteSpace(filterText)
                ? _allLines
                : _allLines.Where(line => line.Text.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

            Lines.Clear();
            for (int i = 0; i < filteredLines.Count; i++)
            {
                BuildSegmentsForLine(filteredLines[i], i);
                Lines.Add(filteredLines[i]);
            }

            UpdateMatchInfo();

            if (_autoScrollEnabled)
            {
                ScrollToLatestLine(force: true);
            }
        }

        private void ApplyIncrementalVisibleUpdate(IReadOnlyList<int> changedLineIndices)
        {
            if (changedLineIndices.Count == 0)
            {
                return;
            }

            var filterText = LogViewerFilterTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                foreach (var lineIndex in changedLineIndices.Distinct().OrderBy(index => index))
                {
                    if (lineIndex < 0 || lineIndex >= _allLines.Count)
                    {
                        continue;
                    }

                    var line = _allLines[lineIndex];
                    BuildSegmentsForLine(line, lineIndex);

                    if (!Lines.Contains(line))
                    {
                        Lines.Add(line);
                    }
                }

                UpdateMatchInfo();
                return;
            }

            var requiresVisibleRefresh = false;
            foreach (var lineIndex in changedLineIndices.Distinct())
            {
                if (lineIndex < 0 || lineIndex >= _allLines.Count)
                {
                    continue;
                }

                var line = _allLines[lineIndex];
                var affectsVisibleList =
                    Lines.Contains(line) ||
                    line.Text.Contains(filterText, StringComparison.OrdinalIgnoreCase);

                if (affectsVisibleList)
                {
                    requiresVisibleRefresh = true;
                    break;
                }
            }

            if (requiresVisibleRefresh)
            {
                RefreshVisibleLines();
            }
            UpdateMatchInfo();
        }

        private void UpdateMatchInfo()
        {
            if (LogViewerMatchesText == null)
            {
                return;
            }

            LogViewerMatchesText.Text = string.Format(T("LOGVIEWER_FILTER_COUNT_FORMAT"), Lines.Count, _allLines.Count);

            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            if (LogViewerReloadButton != null) LogViewerReloadButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentFilePath);
        }

        private void ScrollToLatestLine(bool force = false)
        {
            if (Lines.Count == 0)
            {
                return;
            }

            if (!force && (!_autoScrollEnabled || !_isPinnedToBottom))
            {
                return;
            }

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                EnsureLogListScrollViewer();
                if (_logListScrollViewer != null)
                {
                    _isPinnedToBottom = true;
                    _logListScrollViewer.ChangeView(null, _logListScrollViewer.ScrollableHeight, null, disableAnimation: true);
                }
            });
        }

        private void EnsureLogListScrollViewer()
        {
            if (_logListScrollViewer != null)
            {
                return;
            }

            _logListScrollViewer = FindDescendant<ScrollViewer>(LogViewerListView);
            if (_logListScrollViewer != null)
            {
                _logListScrollViewer.ViewChanged -= LogListScrollViewer_ViewChanged;
                _logListScrollViewer.ViewChanged += LogListScrollViewer_ViewChanged;
                UpdatePinnedToBottomState();
            }
        }

        private void DetachLogListScrollViewer()
        {
            if (_logListScrollViewer == null)
            {
                return;
            }

            _logListScrollViewer.ViewChanged -= LogListScrollViewer_ViewChanged;
            _logListScrollViewer = null;
        }

        private void LogListScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdatePinnedToBottomState();
        }

        private void UpdatePinnedToBottomState()
        {
            if (_logListScrollViewer == null)
            {
                _isPinnedToBottom = true;
                return;
            }

            if (_autoScrollEnabled)
            {
                _isPinnedToBottom = true;
                return;
            }

            const double bottomTolerance = 8;
            _isPinnedToBottom =
                _logListScrollViewer.VerticalOffset >= _logListScrollViewer.ScrollableHeight - bottomTolerance;
        }

        private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var nestedResult = FindDescendant<T>(child);
                if (nestedResult != null)
                {
                    return nestedResult;
                }
            }

            return null;
        }

        private void SetLoadingState(bool isLoading)
        {
            if (LogViewerReloadButton != null) LogViewerReloadButton.IsEnabled = !isLoading && !string.IsNullOrWhiteSpace(_currentFilePath);
        }

        private void LoadRecentFiles()
        {
            RecentFiles.Clear();
            foreach (var path in _logViewerStore.ReadRecentFiles())
            {
                RecentFiles.Add(CreateRecentFileEntry(path));
            }

            if (LogViewerFileComboBox != null)
            {
                LogViewerFileComboBox.ItemsSource = RecentFiles;
            }

            RefreshRecentFileDisplayTexts();
        }

        private void LoadHighlightRules()
        {
            HighlightRules.Clear();
            foreach (var rule in _highlightStore.ReadRules())
            {
                HighlightRules.Add(rule);
            }
        }

        private void AddRecentFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var existingIndex = RecentFiles
                .Select((entry, index) => new { entry, index })
                .FirstOrDefault(item => string.Equals(item.entry.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (existingIndex.HasValue)
            {
                RecentFiles.RemoveAt(existingIndex.Value);
            }

            RecentFiles.Insert(0, CreateRecentFileEntry(filePath));

            while (RecentFiles.Count > 10)
            {
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
            }

            _logViewerStore.WriteRecentFiles(RecentFiles.Select(entry => entry.FullPath));
            RefreshRecentFileDisplayTexts();
            SelectRecentFile(filePath);
        }

        private void SelectRecentFile(string filePath)
        {
            if (LogViewerFileComboBox == null)
            {
                return;
            }

            var selectedEntry = RecentFiles.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, filePath, StringComparison.OrdinalIgnoreCase));

            _suppressFileSelectionChange = true;
            LogViewerFileComboBox.SelectedItem = selectedEntry;
            _suppressFileSelectionChange = false;
        }

        private RecentFileEntry CreateRecentFileEntry(string filePath)
        {
            return new RecentFileEntry
            {
                FullPath = filePath,
                DisplayText = BuildRecentFileDisplayText(filePath, GetRecentFileDisplayMaxLength())
            };
        }

        private void RefreshRecentFileDisplayTexts()
        {
            foreach (var entry in RecentFiles)
            {
                entry.DisplayText = BuildRecentFileDisplayText(entry.FullPath, GetRecentFileDisplayMaxLength());
            }

            if (LogViewerFileComboBox != null)
            {
                var selected = LogViewerFileComboBox.SelectedItem;
                LogViewerFileComboBox.ItemsSource = null;
                LogViewerFileComboBox.ItemsSource = RecentFiles;
                LogViewerFileComboBox.SelectedItem = selected;
            }
        }

        private int GetRecentFileDisplayMaxLength()
        {
            var comboWidth = LogViewerFileComboBox?.ActualWidth ?? 0;
            if (comboWidth <= 0)
            {
                return 38;
            }

            const double reservedDropdownChromeWidth = 34;
            var usableWidth = Math.Max(0, comboWidth - reservedDropdownChromeWidth);
            return Math.Max(14, (int)Math.Floor(usableWidth / 7.2));
        }

        private static string BuildRecentFileDisplayText(string filePath, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return filePath;
            }

            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return filePath;
            }

            if (filePath.Length <= maxLength)
            {
                return filePath;
            }

            if (maxLength <= 3)
            {
                return fileName.Length <= maxLength
                    ? fileName
                    : fileName.Substring(fileName.Length - maxLength, maxLength);
            }

            var visibleTailLength = maxLength - 3;
            if (visibleTailLength >= filePath.Length)
            {
                return filePath;
            }

            return "..." + filePath.Substring(filePath.Length - visibleTailLength, visibleTailLength);
        }

        private void EnsureWatcher(string filePath)
        {
            DisposeWatcher();

            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
            {
                return;
            }

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnWatchedFileChanged;
        }

        private void DisposeWatcher()
        {
            if (_fileWatcher == null)
            {
                return;
            }

            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnWatchedFileChanged;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                return;
            }

            try
            {
                var info = new FileInfo(_currentFilePath);
                if (!info.Exists)
                {
                    return;
                }

                if (info.Length == _lastKnownFileLength && info.LastWriteTimeUtc <= _lastKnownFileWriteTimeUtc)
                {
                    return;
                }
            }
            catch
            {
            }

            _reloadDebounceTimer.Change(450, Timeout.Infinite);
        }

        private void OnReloadDebounceTimerTick(object? state)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(async () =>
            {
                var now = DateTimeOffset.UtcNow;
                if (now - _lastLiveReloadAt < TimeSpan.FromMilliseconds(500))
                {
                    return;
                }

                _lastLiveReloadAt = now;
                await AppendLatestChangesAsync(_currentFilePath);
            });
        }

        private void InitializeHighlightColors()
        {
            HighlightColorOptions.Clear();
            HighlightColorOptions.Add(new HighlightColorOptionItem("red", new SolidColorBrush(Color.FromArgb(255, 140, 0, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("orange", new SolidColorBrush(Color.FromArgb(255, 161, 64, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("amber", new SolidColorBrush(Color.FromArgb(255, 156, 94, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("yellow", new SolidColorBrush(Color.FromArgb(255, 147, 116, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("lime", new SolidColorBrush(Color.FromArgb(255, 84, 110, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("green", new SolidColorBrush(Color.FromArgb(255, 0, 78, 0))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("mint", new SolidColorBrush(Color.FromArgb(255, 0, 96, 66))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("teal", new SolidColorBrush(Color.FromArgb(255, 0, 89, 96))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("cyan", new SolidColorBrush(Color.FromArgb(255, 0, 86, 122))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("sky", new SolidColorBrush(Color.FromArgb(255, 0, 72, 132))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("blue", new SolidColorBrush(Color.FromArgb(255, 0, 49, 98))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("indigo", new SolidColorBrush(Color.FromArgb(255, 42, 47, 122))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("violet", new SolidColorBrush(Color.FromArgb(255, 75, 45, 130))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("purple", new SolidColorBrush(Color.FromArgb(255, 89, 0, 89))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("magenta", new SolidColorBrush(Color.FromArgb(255, 124, 0, 88))));
            HighlightColorOptions.Add(new HighlightColorOptionItem("pink", new SolidColorBrush(Color.FromArgb(255, 132, 24, 80))));
        }

        private (Brush BackgroundBrush, Brush ForegroundBrush) GetRuleBrushes(string colorKey)
        {
            var baseColor = colorKey.ToLowerInvariant() switch
            {
                "orange" => Color.FromArgb(255, 161, 64, 0),
                "amber" => Color.FromArgb(255, 156, 94, 0),
                "yellow" => Color.FromArgb(255, 147, 116, 0),
                "lime" => Color.FromArgb(255, 84, 110, 0),
                "green" => Color.FromArgb(255, 0, 78, 0),
                "mint" => Color.FromArgb(255, 0, 96, 66),
                "teal" => Color.FromArgb(255, 0, 89, 96),
                "cyan" => Color.FromArgb(255, 0, 86, 122),
                "sky" => Color.FromArgb(255, 0, 72, 132),
                "blue" => Color.FromArgb(255, 0, 49, 98),
                "indigo" => Color.FromArgb(255, 42, 47, 122),
                "violet" => Color.FromArgb(255, 75, 45, 130),
                "purple" => Color.FromArgb(255, 89, 0, 89),
                "magenta" => Color.FromArgb(255, 124, 0, 88),
                "pink" => Color.FromArgb(255, 132, 24, 80),
                _ => Color.FromArgb(255, 140, 0, 0)
            };

            return (new SolidColorBrush(baseColor), new SolidColorBrush(Colors.White));
        }

        private sealed record ParsedLogContent(IReadOnlyList<string> Lines, bool EndsWithLineBreak);
        private sealed record LogFileSnapshot(IReadOnlyList<string> Lines, long FileLength, DateTime LastWriteTimeUtc, bool EndsWithLineBreak);
        private sealed record HighlightMatch(int LineIndex, int Start, int Length, int RuleIndex);
    }
}
