using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.Markup;

public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return new Binding($"[{Key}]")
        {
            Source = LocalizationBindingSource.Instance,
            Mode = BindingMode.OneWay
        };
    }
}
