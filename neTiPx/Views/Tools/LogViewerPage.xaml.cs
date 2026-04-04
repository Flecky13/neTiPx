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
using Windows.UI;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace neTiPx.Views
{
    public sealed partial class LogViewerPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly SolidColorBrush _matchForegroundBrush = new SolidColorBrush(Colors.Black);
        private readonly SolidColorBrush _matchBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 255, 232, 153));
        private readonly SolidColorBrush _activeMatchBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 255, 196, 37));
        private readonly SolidColorBrush _defaultBackgroundBrush = new SolidColorBrush(Colors.Transparent);
        private readonly Brush _defaultForegroundBrush;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private readonly List<int> _matchIndices = new List<int>();
        private readonly LogViewerStore _logViewerStore = new LogViewerStore();
        private FileSystemWatcher? _fileWatcher;
        private CancellationTokenSource? _reloadDebounceCts;
        private string _currentFilePath = string.Empty;
        private int _currentMatchPointer = -1;
        private bool _suppressFileSelectionChange;
        private bool _autoScrollEnabled = true;
        private long _lastKnownFileLength;
        private bool _lastLoadEndedWithLineBreak;
        private ScrollViewer? _logListScrollViewer;
        private bool _isPinnedToBottom = true;

        public ObservableCollection<LogViewerLine> Lines { get; } = new ObservableCollection<LogViewerLine>();
        public ObservableCollection<RecentFileEntry> RecentFiles { get; } = new ObservableCollection<RecentFileEntry>();

        public LogViewerPage()
        {
            InitializeComponent();
            _defaultForegroundBrush = Application.Current.Resources["AppTextBrush"] as Brush
                ?? new SolidColorBrush(Colors.White);
            Loaded += LogViewerPage_Loaded;
            Unloaded += LogViewerPage_Unloaded;
        }

        private static string T(string key) => _lm.Lang(key);

        private void LogViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;
            EnsureLogListScrollViewer();
            LoadRecentFiles();
            UpdateLanguage();
            UpdateMatchInfo();
        }

        private void LogViewerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            DetachLogListScrollViewer();
            DisposeWatcher();
            _reloadDebounceCts?.Cancel();
            _reloadDebounceCts?.Dispose();
            _reloadDebounceCts = null;
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
            if (LogViewerHeaderLineText != null) LogViewerHeaderLineText.Text = T("LOGVIEWER_HEADER_LINE");
            if (LogViewerHeaderContentText != null) LogViewerHeaderContentText.Text = T("LOGVIEWER_HEADER_CONTENT");
            if (LogViewerFilterTextBox != null)
            {
                LogViewerFilterTextBox.Header = T("LOGVIEWER_FILTER_HEADER");
                LogViewerFilterTextBox.PlaceholderText = T("LOGVIEWER_FILTER_PLACEHOLDER");
            }
            if (LogViewerAutoRefreshCheckBox != null) LogViewerAutoRefreshCheckBox.Content = T("LOGVIEWER_AUTO_REFRESH");

            if (LogViewerFileComboBox != null && string.IsNullOrWhiteSpace(_currentFilePath))
            {
                LogViewerFileComboBox.PlaceholderText = T("LOGVIEWER_NO_FILE");
            }

            if (LogViewerStatusText != null && string.IsNullOrWhiteSpace(LogViewerStatusText.Text))
            {
                LogViewerStatusText.Text = T("LOGVIEWER_STATUS_READY");
            }

            if (LogViewerReloadButton != null) ToolTipService.SetToolTip(LogViewerReloadButton, T("LOGVIEWER_BUTTON_RELOAD"));
            if (LogViewerPrevButton != null) ToolTipService.SetToolTip(LogViewerPrevButton, T("LOGVIEWER_BUTTON_PREV"));
            if (LogViewerNextButton != null) ToolTipService.SetToolTip(LogViewerNextButton, T("LOGVIEWER_BUTTON_NEXT"));
            if (LogViewerAutoRefreshCheckBox != null) ToolTipService.SetToolTip(LogViewerAutoRefreshCheckBox, T("LOGVIEWER_AUTO_REFRESH"));
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
            ApplyFilterAndRefreshMatches();
        }

        private void LogViewerPrevButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMatch(-1);
        }

        private void LogViewerNextButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMatch(1);
        }

        private async Task LoadFileAsync(string filePath, bool forceStatusMessage, bool scrollToEnd = false)
        {
            await _loadLock.WaitAsync();
            try
            {
                SetLoadingState(true);
                UpdateStatus(T("LOGVIEWER_STATUS_LOADING"));
                var fileSnapshot = await ReadAllLinesWithRetryAsync(filePath);

                _currentFilePath = filePath;
                _lastKnownFileLength = fileSnapshot.FileLength;
                _lastLoadEndedWithLineBreak = fileSnapshot.EndsWithLineBreak;
                SelectRecentFile(filePath);
                ToolTipService.SetToolTip(LogViewerFileComboBox, filePath);

                Lines.Clear();
                for (int i = 0; i < fileSnapshot.Lines.Count; i++)
                {
                    Lines.Add(new LogViewerLine
                    {
                        LineNumber = i + 1,
                        Text = fileSnapshot.Lines[i],
                        BackgroundBrush = _defaultBackgroundBrush,
                        ForegroundBrush = _defaultForegroundBrush
                    });
                }

                EnsureWatcher(filePath);
                ApplyFilterAndRefreshMatches();

                if (scrollToEnd)
                {
                    ScrollToLatestLine(force: true);
                }

                if (forceStatusMessage)
                {
                    UpdateStatus(string.Format(T("LOGVIEWER_STATUS_LINES_LOADED"), fileSnapshot.Lines.Count));
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"{T("LOGVIEWER_STATUS_ERROR")}: {ex.Message}");
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
                    if (currentLength < _lastKnownFileLength)
                    {
                        requiresFullReload = true;
                    }
                    else if (currentLength == _lastKnownFileLength)
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
                        _lastLoadEndedWithLineBreak = parsedChunk.EndsWithLineBreak;

                        if (string.IsNullOrWhiteSpace(LogViewerFilterTextBox?.Text))
                        {
                            _matchIndices.Clear();
                            _currentMatchPointer = -1;
                            UpdateMatchInfo();
                        }
                        else
                        {
                            ApplyIncrementalFilterUpdate(changedLineIndices);
                        }

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
                    return new LogFileSnapshot(parsedContent.Lines, stream.Length, parsedContent.EndsWithLineBreak);
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
            var changedIndices = new List<int>();
            if (parsedChunk.Lines.Count == 0)
            {
                return changedIndices;
            }

            var appendStartIndex = 0;

            if (!_lastLoadEndedWithLineBreak && Lines.Count > 0)
            {
                var lastLine = Lines[^1];
                lastLine.Text += parsedChunk.Lines[0];
                changedIndices.Add(Lines.Count - 1);
                appendStartIndex = 1;
            }

            for (int i = appendStartIndex; i < parsedChunk.Lines.Count; i++)
            {
                Lines.Add(new LogViewerLine
                {
                    LineNumber = Lines.Count + 1,
                    Text = parsedChunk.Lines[i],
                    BackgroundBrush = _defaultBackgroundBrush,
                    ForegroundBrush = _defaultForegroundBrush
                });
                changedIndices.Add(Lines.Count - 1);
            }

            return changedIndices;
        }

        private void ApplyFilterAndRefreshMatches()
        {
            var filter = LogViewerFilterTextBox?.Text?.Trim() ?? string.Empty;
            _matchIndices.Clear();

            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                var isMatch = !string.IsNullOrWhiteSpace(filter)
                    && line.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);

                line.IsMatch = isMatch;
                line.IsActiveMatch = false;
                ApplyLineVisualState(line);

                if (isMatch)
                {
                    _matchIndices.Add(i);
                }
            }

            SetActiveMatchPointer(_matchIndices.Count > 0 ? 0 : -1, scrollIntoView: true);
        }

        private void ApplyIncrementalFilterUpdate(IReadOnlyList<int> changedLineIndices)
        {
            var filter = LogViewerFilterTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filter) || changedLineIndices.Count == 0)
            {
                UpdateMatchInfo();
                return;
            }

            var firstMatchAppeared = _matchIndices.Count == 0;

            foreach (var index in changedLineIndices.Distinct().OrderBy(index => index))
            {
                if (index < 0 || index >= Lines.Count)
                {
                    continue;
                }

                var line = Lines[index];
                var isMatch = line.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
                var existingMatchPosition = _matchIndices.IndexOf(index);

                if (existingMatchPosition >= 0 && !isMatch)
                {
                    _matchIndices.RemoveAt(existingMatchPosition);
                    if (_currentMatchPointer > existingMatchPosition)
                    {
                        _currentMatchPointer--;
                    }
                    else if (_currentMatchPointer == existingMatchPosition)
                    {
                        _currentMatchPointer = Math.Min(_currentMatchPointer, _matchIndices.Count - 1);
                    }
                }
                else if (existingMatchPosition < 0 && isMatch)
                {
                    _matchIndices.Add(index);
                }

                line.IsMatch = isMatch;
                line.IsActiveMatch = false;
                ApplyLineVisualState(line);
            }

            if (firstMatchAppeared && _matchIndices.Count > 0)
            {
                SetActiveMatchPointer(0, scrollIntoView: true);
                return;
            }

            if (_currentMatchPointer >= _matchIndices.Count)
            {
                SetActiveMatchPointer(_matchIndices.Count - 1, scrollIntoView: false);
                return;
            }

            if (_currentMatchPointer >= 0 && _currentMatchPointer < _matchIndices.Count)
            {
                var activeIndex = _matchIndices[_currentMatchPointer];
                if (activeIndex >= 0 && activeIndex < Lines.Count)
                {
                    var activeLine = Lines[activeIndex];
                    activeLine.IsActiveMatch = true;
                    ApplyLineVisualState(activeLine);
                }
            }

            UpdateMatchInfo();
        }

        private void NavigateToMatch(int direction)
        {
            if (_matchIndices.Count == 0)
            {
                return;
            }

            if (_currentMatchPointer < 0)
            {
                _currentMatchPointer = 0;
            }
            else
            {
                _currentMatchPointer = (_currentMatchPointer + direction + _matchIndices.Count) % _matchIndices.Count;
            }

            SetActiveMatchPointer(_currentMatchPointer, scrollIntoView: true);
        }

        private void UpdateMatchInfo()
        {
            if (LogViewerMatchesText == null)
            {
                return;
            }

            if (_matchIndices.Count == 0)
            {
                LogViewerMatchesText.Text = T("LOGVIEWER_MATCHES_NONE");
            }
            else
            {
                LogViewerMatchesText.Text = string.Format(T("LOGVIEWER_MATCHES_FORMAT"), _currentMatchPointer + 1, _matchIndices.Count);
            }

            UpdateNavigationButtons();
        }

        private void SetActiveMatchPointer(int pointer, bool scrollIntoView)
        {
            var previousPointer = _currentMatchPointer;
            if (previousPointer >= 0 && previousPointer < _matchIndices.Count)
            {
                var previousIndex = _matchIndices[previousPointer];
                if (previousIndex >= 0 && previousIndex < Lines.Count)
                {
                    var previousLine = Lines[previousIndex];
                    previousLine.IsActiveMatch = false;
                    ApplyLineVisualState(previousLine);
                }
            }

            _currentMatchPointer = pointer >= 0 && pointer < _matchIndices.Count ? pointer : -1;

            if (_currentMatchPointer >= 0)
            {
                var targetIndex = _matchIndices[_currentMatchPointer];
                if (targetIndex >= 0 && targetIndex < Lines.Count)
                {
                    var targetLine = Lines[targetIndex];
                    targetLine.IsActiveMatch = true;
                    ApplyLineVisualState(targetLine);

                    if (scrollIntoView)
                    {
                        LogViewerListView.ScrollIntoView(targetLine, ScrollIntoViewAlignment.Leading);
                    }
                }
            }

            UpdateMatchInfo();
        }

        private void ApplyLineVisualState(LogViewerLine line)
        {
            if (line.IsActiveMatch)
            {
                line.BackgroundBrush = _activeMatchBackgroundBrush;
                line.ForegroundBrush = _matchForegroundBrush;
            }
            else if (line.IsMatch)
            {
                line.BackgroundBrush = _matchBackgroundBrush;
                line.ForegroundBrush = _matchForegroundBrush;
            }
            else
            {
                line.BackgroundBrush = _defaultBackgroundBrush;
                line.ForegroundBrush = _defaultForegroundBrush;
            }
        }

        private void UpdateNavigationButtons()
        {
            var hasMatches = _matchIndices.Count > 0;

            if (LogViewerPrevButton != null) LogViewerPrevButton.IsEnabled = hasMatches;
            if (LogViewerNextButton != null) LogViewerNextButton.IsEnabled = hasMatches;
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

            var lastLine = Lines[Lines.Count - 1];
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                LogViewerListView?.UpdateLayout();
                EnsureLogListScrollViewer();

                if (_logListScrollViewer != null)
                {
                    _logListScrollViewer.ChangeView(null, _logListScrollViewer.ScrollableHeight, null, disableAnimation: true);
                }

                LogViewerListView.ScrollIntoView(lastLine, ScrollIntoViewAlignment.Leading);
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

        private void LogListScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
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

        private void UpdateStatus(string text)
        {
            if (LogViewerStatusText != null)
            {
                LogViewerStatusText.Text = text;
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            if (LogViewerProgressRing != null) LogViewerProgressRing.IsActive = isLoading;
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
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnWatchedFileChanged;
            _fileWatcher.Created += OnWatchedFileChanged;
            _fileWatcher.Renamed += OnWatchedFileChanged;
        }

        private void DisposeWatcher()
        {
            if (_fileWatcher == null)
            {
                return;
            }

            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnWatchedFileChanged;
            _fileWatcher.Created -= OnWatchedFileChanged;
            _fileWatcher.Renamed -= OnWatchedFileChanged;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                return;
            }

            _reloadDebounceCts?.Cancel();
            _reloadDebounceCts?.Dispose();
            _reloadDebounceCts = new CancellationTokenSource();
            var token = _reloadDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        UpdateStatus(T("LOGVIEWER_STATUS_LIVE_RELOADING"));
                        await AppendLatestChangesAsync(_currentFilePath);
                        UpdateStatus(T("LOGVIEWER_STATUS_LIVE_UPDATED"));
                    });
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        private sealed record ParsedLogContent(IReadOnlyList<string> Lines, bool EndsWithLineBreak);

        private sealed record LogFileSnapshot(IReadOnlyList<string> Lines, long FileLength, bool EndsWithLineBreak);
    }
}
