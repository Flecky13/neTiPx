using Avalonia.Controls;
using Avalonia.Interactivity;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.Views;

public partial class PagesVisibilityWindow : Window
{
    private readonly PageVisibilityStore _store = new();

    public PagesVisibilityWindow()
    {
        InitializeComponent();
        LocalizeLabels();
        LoadConfig();
        WireToolsDependency();
    }

    private void LocalizeLabels()
    {
        var lm = LanguageManager.Instance;
        Title               = lm.Lang("PAGES_VIS_TITLE");
        TxtTitle.Text       = lm.Lang("PAGES_VIS_TITLE");
        TxtDesc.Text        = lm.Lang("PAGES_VIS_DESC");
        TxtGroupMain.Text   = lm.Lang("PAGES_VIS_GROUP_MAIN");
        TxtGroupTools.Text  = lm.Lang("PAGES_VIS_GROUP_TOOLS");
        BtnCancel.Content   = lm.Lang("PAGES_VIS_CANCEL");
        BtnApply.Content    = lm.Lang("PAGES_VIS_APPLY");
        CbAdapters.Content      = "🔌 " + lm.Lang("NAV_ADAPTERS");
        CbIpConfig.Content      = "🧩 " + lm.Lang("NAV_IPCONFIG");
        CbRoutes.Content        = "🧭 " + lm.Lang("TOOLS_ROUTES");
        CbUncPath.Content       = "📁 " + lm.Lang("TOOLS_UNC_PATH");
        CbTools.Content         = "🧰 " + lm.Lang("NAV_TOOLS");
        CbToolNetCalc.Content   = "🔢 " + lm.Lang("TOOLS_NET_CALC");
        CbToolPing.Content      = "📡 " + lm.Lang("TOOLS_PING");
        CbToolWlan.Content      = "📶 " + lm.Lang("TOOLS_WLAN");
        CbToolNetScan.Content   = "🔍 " + lm.Lang("TOOLS_NET_SCAN");
        CbToolLogViewer.Content = "📄 " + lm.Lang("TOOLS_LOG_VIEWER");
    }

    private void LoadConfig()
    {
        var cfg = _store.Read();
        CbIpConfig.IsChecked       = cfg.ShowIpConfig;
        CbRoutes.IsChecked         = cfg.ShowRoutes;
        CbUncPath.IsChecked        = cfg.ShowUncPath;
        CbTools.IsChecked          = cfg.ShowTools;
        CbToolNetCalc.IsChecked    = cfg.ShowToolNetCalc;
        CbToolPing.IsChecked       = cfg.ShowToolPing;
        CbToolWlan.IsChecked       = cfg.ShowToolWlan;
        CbToolNetScan.IsChecked    = cfg.ShowToolNetScan;
        CbToolLogViewer.IsChecked  = cfg.ShowToolLogViewer;
        UpdateToolGroupEnabled(cfg.ShowTools);
    }

    // Abhängigkeit: Werkzeuge-Gruppe nur aktiv wenn "Werkzeuge" sichtbar ist.
    private void WireToolsDependency()
    {
        CbTools.IsCheckedChanged += (_, _) => UpdateToolGroupEnabled(CbTools.IsChecked == true);
    }

    private void UpdateToolGroupEnabled(bool toolsVisible)
    {
        CbToolNetCalc.IsEnabled   = toolsVisible;
        CbToolPing.IsEnabled      = toolsVisible;
        CbToolWlan.IsEnabled      = toolsVisible;
        CbToolNetScan.IsEnabled   = toolsVisible;
        CbToolLogViewer.IsEnabled = toolsVisible;
    }

    private void BtnApply_Click(object? sender, RoutedEventArgs e)
    {
        _store.Write(new PageVisibilityStore.Config
        {
            ShowIpConfig       = CbIpConfig.IsChecked      == true,
            ShowRoutes         = CbRoutes.IsChecked         == true,
            ShowUncPath        = CbUncPath.IsChecked        == true,
            ShowTools          = CbTools.IsChecked          == true,
            ShowToolNetCalc    = CbToolNetCalc.IsChecked    == true,
            ShowToolPing       = CbToolPing.IsChecked       == true,
            ShowToolWlan       = CbToolWlan.IsChecked       == true,
            ShowToolNetScan    = CbToolNetScan.IsChecked    == true,
            ShowToolLogViewer  = CbToolLogViewer.IsChecked  == true,
        });
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
