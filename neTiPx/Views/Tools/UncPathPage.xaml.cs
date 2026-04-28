using System.Linq;
using neTiPx.Models;
using neTiPx.Services;
using neTiPx.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace neTiPx.Views.Tools;

/// <summary>
/// UNC-Pfad-Profil-Verwaltungsseite
/// </summary>
public sealed partial class UncPathPage : Page
{
    private static readonly LanguageManager _lm = LanguageManager.Instance;
    private readonly UncPathViewModel _viewModel = new();
    private bool _isHandlingSelection;

    public UncPathPage()
    {
        this.InitializeComponent();
        this.DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is UncPathViewModel viewModel)
            ProfileListView.SelectedItem = viewModel.SelectedProfile;
    }

    private async void ProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHandlingSelection || DataContext is not UncPathViewModel viewModel)
            return;

        var currentProfile = viewModel.SelectedProfile;
        var nextProfile = e.AddedItems.OfType<UncPathProfile>().FirstOrDefault();

        if (nextProfile == null || ReferenceEquals(nextProfile, currentProfile))
            return;

        _isHandlingSelection = true;

        try
        {
            if (currentProfile?.IsDirty == true)
            {
                var dialog = new ContentDialog
                {
                    Title = _lm.Lang("IPCONFIG_UNSAVED_TITLE"),
                    Content = _lm.Lang("IPCONFIG_UNSAVED_CONTENT"),
                    PrimaryButtonText = _lm.Lang("IPCONFIG_SAVE"),
                    CloseButtonText = _lm.Lang("IPCONFIG_NO"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (!viewModel.SaveCurrentProfileForProfileSwitch())
                    {
                        ProfileListView.SelectedItem = currentProfile;
                        return;
                    }

                    currentProfile.IsDirty = false;
                }
                else
                {
                    currentProfile.IsDirty = false;
                    viewModel.DiscardCurrentProfileChangesMarker();
                }
            }

            viewModel.SelectedProfile = nextProfile;
            ProfileListView.SelectedItem = nextProfile;
        }
        finally
        {
            _isHandlingSelection = false;
        }
    }

    private void PasswordBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        SyncPasswordBoxWithEntry(passwordBox);
    }

    private void PasswordBox_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        SyncPasswordBoxWithEntry(passwordBox);
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        if (passwordBox.DataContext is UncPathEntry entry && entry.Password != passwordBox.Password)
            entry.Password = passwordBox.Password;
    }

    private static void SyncPasswordBoxWithEntry(PasswordBox passwordBox)
    {
        if (passwordBox.DataContext is not UncPathEntry entry)
            return;

        var expectedPassword = entry.Password ?? string.Empty;
        if (passwordBox.Password != expectedPassword)
            passwordBox.Password = expectedPassword;
    }
}
