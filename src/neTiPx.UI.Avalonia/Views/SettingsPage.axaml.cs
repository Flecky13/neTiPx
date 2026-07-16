using System;
using System.Collections.Generic;
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

            await ViewModel.ImportSettingsArchiveAsync(selected[0].Path.LocalPath);
            await ShowMessageDialog("Import erfolgreich", "Die Einstellungen wurden aus der ZIP-Datei importiert.");
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
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
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
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
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
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
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
