using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;

namespace neTiPx.Models
{
    public sealed class LogViewerLine : ObservableObject
    {
        private string _text = string.Empty;
        private bool _isMatch;
        private bool _isActiveMatch;
        private Brush? _backgroundBrush;
        private Brush? _foregroundBrush;

        public int LineNumber { get; set; }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public bool IsMatch
        {
            get => _isMatch;
            set => SetProperty(ref _isMatch, value);
        }

        public bool IsActiveMatch
        {
            get => _isActiveMatch;
            set => SetProperty(ref _isActiveMatch, value);
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
