namespace neTiPx.Models
{
    public sealed class RecentFileEntry
    {
        public string FullPath { get; set; } = string.Empty;

        public string DisplayText { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
