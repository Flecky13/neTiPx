using neTiPx.Helpers;

namespace neTiPx.Models
{
    public sealed class LogViewerHighlightRule : ObservableObject
    {
        private string _searchText = string.Empty;
        private string _colorKey = "red";

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public string ColorKey
        {
            get => _colorKey;
            set => SetProperty(ref _colorKey, value);
        }
    }
}
