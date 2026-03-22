using Microsoft.UI.Xaml.Controls;
using neTiPx.Services;
using System;
using Microsoft.UI.Xaml;

namespace neTiPx.Views
{
    public partial class InfoPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;

        public InfoPage()
        {
            InitializeComponent();
            Loaded += InfoPage_Loaded;
            Unloaded += InfoPage_Unloaded;
        }

        private void InfoPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;
            UpdateLanguage();
        }

        private void InfoPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (InfoTitleText != null) InfoTitleText.Text = _lm.Lang("INFO_TITLE");
            if (InfoSubtitleText != null) InfoSubtitleText.Text = _lm.Lang("INFO_SUBTITLE");
            if (InstalledVersionLabel != null) InstalledVersionLabel.Text = _lm.Lang("INFO_INSTALLED_VERSION");
            if (LatestVersionLabel != null) LatestVersionLabel.Text = _lm.Lang("INFO_LATEST_VERSION");
            if (UpdateStatusLabel != null) UpdateStatusLabel.Text = _lm.Lang("INFO_UPDATE_STATUS");
            if (GitHubReleasesLink != null) GitHubReleasesLink.Content = _lm.Lang("INFO_GITHUB_RELEASES");
            if (LatestReleaseLink != null) LatestReleaseLink.Content = _lm.Lang("INFO_LATEST_RELEASE");
            if (CheckUpdateButtonText != null) CheckUpdateButtonText.Text = _lm.Lang("INFO_CHECK_UPDATE");
            if (InstallUpdateButtonText != null) InstallUpdateButtonText.Text = _lm.Lang("INFO_INSTALL_UPDATE");
            if (ChangelogButtonText != null) ChangelogButtonText.Text = _lm.Lang("INFO_CHANGELOG");
            if (HelpButtonText != null) HelpButtonText.Text = _lm.Lang("INFO_HELP");
            if (LicenseTitleText != null) LicenseTitleText.Text = _lm.Lang("INFO_LICENSE_TITLE");
            if (LicenseText != null) LicenseText.Text = _lm.Lang("INFO_LICENSE_TEXT");
            if (ContactLabel != null) ContactLabel.Text = _lm.Lang("INFO_CONTACT");
            if (SupportLabel != null) SupportLabel.Text = _lm.Lang("INFO_SUPPORT");
        }
    }
}
