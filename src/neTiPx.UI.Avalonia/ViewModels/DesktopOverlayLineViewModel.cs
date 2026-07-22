namespace neTiPx.UI.Avalonia.ViewModels;

public sealed class DesktopOverlayLineViewModel
{
    public DesktopOverlayLineViewModel(string key, string text)
    {
        Key = key;
        Text = text;
    }

    public string Key { get; }
    public string Text { get; }
}
