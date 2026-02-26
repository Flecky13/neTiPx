using System.Collections.ObjectModel;
using System.Linq;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Services;

namespace neTiPx.WinUI.ViewModels
{
    public sealed class ShellViewModel : ObservableObject
    {
        private ThemeOptionItem? _selectedThemeOption;

        public ShellViewModel()
        {
            ThemeOptions = new ObservableCollection<ThemeOptionItem>
            {
                new ThemeOptionItem("System", ThemeOption.System),
                new ThemeOptionItem("Light", ThemeOption.Light),
                new ThemeOptionItem("Dark", ThemeOption.Dark)
            };

            var current = App.ThemeService.CurrentTheme;
            SelectedThemeOption = ThemeOptions.FirstOrDefault(t => t.Value == current) ?? ThemeOptions[0];
        }

        public ObservableCollection<ThemeOptionItem> ThemeOptions { get; }

        public ThemeOptionItem? SelectedThemeOption
        {
            get => _selectedThemeOption;
            set => SetProperty(ref _selectedThemeOption, value);
        }
    }
}
