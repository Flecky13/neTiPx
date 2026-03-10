using Microsoft.UI.Xaml.Controls;
using neTiPx.Models;
using neTiPx.ViewModels;
using System.Linq;

namespace neTiPx.Views
{
    public partial class IpConfigPage : Page
    {
        private bool _isHandlingSelection;

        public IpConfigPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (DataContext is IpConfigViewModel viewModel)
            {
                viewModel.StartConnectionMonitoring();
                ProfileListView.SelectedItem = viewModel.SelectedProfile;
            }
        }

        private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (DataContext is IpConfigViewModel viewModel)
            {
                viewModel.StopConnectionMonitoring();
            }
        }

        private async void ProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || DataContext is not IpConfigViewModel viewModel)
            {
                return;
            }

            var currentProfile = viewModel.SelectedProfile;
            var nextProfile = e.AddedItems.OfType<IpProfile>().FirstOrDefault();

            if (nextProfile == null || ReferenceEquals(nextProfile, currentProfile))
            {
                return;
            }

            _isHandlingSelection = true;
            try
            {
                if (currentProfile?.IsDirty == true)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Ungespeicherte Änderungen",
                        Content = "Änderungen speichern, bevor das Profil verlassen wird?",
                        PrimaryButtonText = "Speichern",
                        CloseButtonText = "Nein",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot
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
    }
}
