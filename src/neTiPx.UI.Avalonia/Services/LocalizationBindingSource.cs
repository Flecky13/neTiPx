using System;
using System.ComponentModel;

namespace neTiPx.UI.Avalonia.Services;

public sealed class LocalizationBindingSource : INotifyPropertyChanged
{
    public static LocalizationBindingSource Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationBindingSource()
    {
        LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public string this[string key] => LanguageManager.Instance.Lang(key);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
