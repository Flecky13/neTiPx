using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;

namespace neTiPx.Models
{
    public sealed class LogViewerTextSegment : ObservableObject
    {
        private string _text = string.Empty;
        private Brush? _backgroundBrush;
        private Brush? _foregroundBrush;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public Brush? BackgroundBrush
        {
            get => _backgroundBrush;
            set => SetProperty(ref _backgroundBrush, value);
        }

        public Brush? ForegroundBrush
        {
            get => _foregroundBrush;
            set => SetProperty(ref _foregroundBrush, value);
        }
    }
}
