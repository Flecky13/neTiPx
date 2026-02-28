using System.Reflection;
using neTiPx.Helpers;

namespace neTiPx.ViewModels
{
    public sealed class InfoViewModel : ObservableObject
    {
        public InfoViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version == null ? "Unknown" : version.ToString();
        }

        public string AppName => "neTiPx";

        public string AppVersion { get; }
    }
}
