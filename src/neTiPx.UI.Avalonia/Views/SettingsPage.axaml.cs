using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class SettingsPage : UserControl
{
    private static readonly FilePickerFileType ZipFileType = new("ZIP")
    {
        Patterns = new[] { "*.zip" }
    };

    public SettingsViewModel ViewModel { get; }
    
    public SettingsPage()
    {
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
        
        InitializeComponent();
    }

    private async void WuenschenButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new PagesVisibilityWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var topLevel = TopLevel.GetTopLevel(this);
        bool applied = false;
        if (topLevel is Window parentWindow)
            applied = await dialog.ShowDialog<bool>(parentWindow);
        else
            dialog.Show();

        if (applied)
            App.MainViewModel?.RebuildNavigation();
    }

    private async void OpenDesktopOverlaySettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new DesktopOverlaySettingsWindow
        {
            DataContext = ViewModel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    private async void ExportSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { CanSave: true } storageProvider)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var selected = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Einstellungen exportieren",
                SuggestedFileName = $"neTiPx-config-{timestamp}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new List<FilePickerFileType> { ZipFileType }
            });

            if (selected == null || selected.Path.IsFile == false)
            {
                return;
            }

            await ViewModel.ExportSettingsArchiveAsync(selected.Path.LocalPath);
            await ShowMessageDialog("Export erfolgreich", "Die Einstellungen wurden als ZIP-Datei exportiert.");
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Export fehlgeschlagen", ex.Message);
        }
    }

    private async void ImportSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { CanOpen: true } storageProvider)
            {
                return;
            }

            var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Einstellungen importieren",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { ZipFileType }
            });

            if (selected.Count == 0 || selected[0].Path.IsFile == false)
            {
                return;
            }

            var entries = await ViewModel.GetImportableSettingsArchiveEntriesAsync(selected[0].Path.LocalPath);
            if (entries.Count == 0)
            {
                await ShowMessageDialog("Import nicht möglich", "Die ZIP-Datei enthält keine importierbaren Einstellungen.");
                return;
            }

            var selectedEntries = await ShowImportSelectionDialog(entries);
            if (selectedEntries == null)
            {
                return;
            }

            if (selectedEntries.Count == 0)
            {
                await ShowMessageDialog("Import abgebrochen", "Es wurde kein Bereich für den Import ausgewählt.");
                return;
            }

            await ViewModel.ImportSettingsArchiveAsync(selected[0].Path.LocalPath, selectedEntries);
            await ShowMessageDialog("Import erfolgreich", "Die ausgewählten Einstellungen wurden importiert. neTiPx wird jetzt neu gestartet.");
            RestartApplication();
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Import fehlgeschlagen", ex.Message);
        }
    }

    private async void ResetSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var confirmed = await ShowConfirmDialog(
            "Einstellungen zurücksetzen",
            "Alle Konfigurationsdateien werden zurückgesetzt. Fortfahren?");

        if (!confirmed)
        {
            return;
        }

        var backupDecision = await ShowYesNoCancelDialog(
            "Backup erstellen",
            "Soll vor dem Zurücksetzen ein Backup als ZIP erstellt werden?");

        if (backupDecision == null)
        {
            return;
        }

        string? backupPath = null;

        if (backupDecision == true)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not { CanSave: true } storageProvider)
            {
                await ShowMessageDialog("Backup nicht möglich", "Dateidialog zum Speichern ist nicht verfügbar.");
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var selected = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Backup speichern",
                SuggestedFileName = $"neTiPx-config-backup-{timestamp}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new List<FilePickerFileType> { ZipFileType }
            });

            if (selected == null || selected.Path.IsFile == false)
            {
                return;
            }

            backupPath = selected.Path.LocalPath;
        }

        try
        {
            var createdBackupPath = await ViewModel.ResetSettingsDataAsync(backupPath);
            if (string.IsNullOrWhiteSpace(createdBackupPath))
            {
                await ShowMessageDialog("Zurücksetzen erfolgreich", "Einstellungen wurden ohne Backup zurückgesetzt.");
            }
            else
            {
                await ShowMessageDialog("Zurücksetzen erfolgreich", $"Einstellungen wurden zurückgesetzt. Backup: {createdBackupPath}");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageDialog("Zurücksetzen fehlgeschlagen", ex.Message);
        }
    }

    private async Task<bool?> ShowYesNoCancelDialog(string title, string message)
    {
        bool? result = null;
        Window? dialog = null;

        dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            new Button
                            {
                                Content = "Abbrechen",
                                Width = 110,
                                Command = new DialogRelayCommand(() =>
                                {
                                    result = null;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Nein",
                                Width = 110,
                                Command = new DialogRelayCommand(() =>
                                {
                                    result = false;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Ja",
                                Width = 110,
                                Classes = { "accent" },
                                Command = new DialogRelayCommand(() =>
                                {
                                    result = true;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return result;
    }

    private async Task<IReadOnlyList<string>?> ShowImportSelectionDialog(IReadOnlyList<string> entries)
    {
        List<string>? result = null;
        Window? dialog = null;

        var listPanel = new StackPanel { Spacing = 8 };
        var checkBoxes = new List<CheckBox>();

        foreach (var entry in entries)
        {
            var checkBox = new CheckBox
            {
                Content = entry,
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 2)
            };

            checkBoxes.Add(checkBox);
            listPanel.Children.Add(checkBox);
        }

        var headerText = new TextBlock
        {
            Text = "Wähle aus, welche Einstellungen importiert werden sollen.",
            TextWrapping = TextWrapping.Wrap
        };

        var entriesBorder = new Border
        {
            Margin = new Thickness(0, 14, 0, 14),
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("SystemControlForegroundBaseLowBrush", Color.Parse("#CCCCCC")),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                Content = listPanel
            }
        };
        Grid.SetRow(entriesBorder, 1);

        var allButton = new Button
        {
            Content = "Alle",
            Width = 110,
            Command = new DialogRelayCommand(() =>
            {
                foreach (var cb in checkBoxes)
                {
                    cb.IsChecked = true;
                }
            })
        };

        var noneButton = new Button
        {
            Content = "Keine",
            Width = 110,
            Command = new DialogRelayCommand(() =>
            {
                foreach (var cb in checkBoxes)
                {
                    cb.IsChecked = false;
                }
            })
        };
        Grid.SetColumn(noneButton, 1);

        var cancelButton = new Button
        {
            Content = "Abbrechen",
            Width = 120,
            Command = new DialogRelayCommand(() =>
            {
                result = null;
                dialog?.Close();
            })
        };
        Grid.SetColumn(cancelButton, 3);

        var importButton = new Button
        {
            Content = "Importieren",
            Width = 120,
            Classes = { "accent" },
            Command = new DialogRelayCommand(() =>
            {
                result = checkBoxes
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Content?.ToString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Cast<string>()
                    .ToList();
                dialog?.Close();
            })
        };
        Grid.SetColumn(importButton, 4);

        var actionsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                allButton,
                noneButton,
                cancelButton,
                importButton
            }
        };
        Grid.SetRow(actionsGrid, 2);

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(20),
            Children =
            {
                headerText,
                entriesBorder,
                actionsGrid
            }
        };

        dialog = new Window
        {
            Title = "Import auswählen",
            Width = 760,
            Height = 560,
            MinWidth = 680,
            MinHeight = 460,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = contentGrid
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return result;
    }

    private async Task<bool> ShowConfirmDialog(string title, string message)
    {
        bool result = false;
        Window? dialog = null;

        dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            new Button
                            {
                                Content = "Abbrechen",
                                Width = 110,
                                Command = new DialogRelayCommand(() =>
                                {
                                    result = false;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Zurücksetzen",
                                Width = 110,
                                Classes = { "accent" },
                                Command = new DialogRelayCommand(() =>
                                {
                                    result = true;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return result;
    }

    private async Task ShowMessageDialog(string title, string message)
    {
        Window? dialog = null;

        dialog = new Window
        {
            Title = title,
            Width = 500,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        Width = 110,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Command = new DialogRelayCommand(() => dialog?.Close())
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    private static IBrush GetThemeBrush(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }

    private static void RestartApplication()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("Die Anwendung konnte nicht neu gestartet werden, da der Prozesspfad nicht verfügbar ist.");
        }

        StartDelayedRelaunch(executablePath, Environment.GetCommandLineArgs().Skip(1));
        App.RequestUserExit();
    }

    private static void StartDelayedRelaunch(string executablePath, IEnumerable<string> args)
    {
        var argList = args.ToList();
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;

        ProcessStartInfo processStartInfo;

        if (OperatingSystem.IsWindows())
        {
            var command = new List<string>
            {
                "timeout /T 1 /NOBREAK >NUL",
                $"start \"\" {QuoteForCmd(executablePath)}"
            };

            if (argList.Count > 0)
            {
                command[1] += " " + string.Join(" ", argList.Select(QuoteForCmd));
            }

            processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + string.Join(" & ", command),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            var command = QuoteForPosixShell(executablePath);
            if (argList.Count > 0)
            {
                command += " " + string.Join(" ", argList.Select(QuoteForPosixShell));
            }

            var scriptPath = Path.Combine(Path.GetTempPath(), $"netipx-restart-{Guid.NewGuid():N}.sh");
            var scriptContent = string.Join('\n',
            [
                "#!/bin/sh",
                "sleep 1",
                $"{command} >/dev/null 2>&1 &",
                "rm -f -- \"$0\"",
                "exit 0"
            ]);

            File.WriteAllText(scriptPath, scriptContent);
            var chmodStartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            chmodStartInfo.ArgumentList.Add("+x");
            chmodStartInfo.ArgumentList.Add(scriptPath);
            _ = Process.Start(chmodStartInfo)?.WaitForExit(1000);

            processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processStartInfo.ArgumentList.Add(scriptPath);
        }

        if (Process.Start(processStartInfo) == null)
        {
            throw new InvalidOperationException("Der Neustart-Prozess konnte nicht gestartet werden.");
        }
    }

    private static string QuoteCommandLineArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (!arg.Any(char.IsWhiteSpace) && !arg.Contains('"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string QuoteForPosixShell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static string QuoteForCmd(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private class DialogRelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;

        public DialogRelayCommand(Action execute)
        {
            _execute = execute;
        }

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
