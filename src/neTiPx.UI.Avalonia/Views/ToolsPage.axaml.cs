using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.Views.Tools;

namespace neTiPx.UI.Avalonia.Views;

public partial class ToolsPage : UserControl
{
    private static readonly FontFamily _emojiFont =
        new("Segoe UI Emoji, Noto Color Emoji, Apple Color Emoji, Segoe UI Symbol");

    public ToolsPage()
    {
        InitializeComponent();
        BuildToolItems();
    }

    private void BuildToolItems()
    {
        var vis = new PageVisibilityStore().Read();
        var lm  = LanguageManager.Instance;

        ToolsListBox.Items.Clear();

        if (vis.ShowToolNetCalc)   ToolsListBox.Items.Add(MakeItem("NetworkCalculator", "🔢", lm.Lang("TOOLS_NET_CALC")));
        if (vis.ShowToolPing)      ToolsListBox.Items.Add(MakeItem("Ping",              "📡", lm.Lang("TOOLS_PING")));
        if (vis.ShowToolWlan)      ToolsListBox.Items.Add(MakeItem("Wlan",              "📶", lm.Lang("TOOLS_WLAN") + " (Draft)"));
        if (vis.ShowToolNetScan)   ToolsListBox.Items.Add(MakeItem("NetworkScanner",    "🔍", lm.Lang("TOOLS_NET_SCAN") + " (Draft)"));
        if (vis.ShowToolLogViewer) ToolsListBox.Items.Add(MakeItem("LogViewer",         "📄", lm.Lang("TOOLS_LOG_VIEWER") + " (Draft)"));

        if (ToolsListBox.ItemCount > 0)
        {
            ToolsListBox.SelectedIndex = 0;
            if (ToolsListBox.SelectedItem is ListBoxItem first && first.Tag is string t)
                UpdateToolContent(t);
        }
    }

    private ListBoxItem MakeItem(string tag, string emoji, string label)
    {
        return new ListBoxItem
        {
            Tag = tag,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = emoji,
                        FontFamily = _emojiFont,
                        FontSize = 16,
                        Width = 20,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }

    private void ToolsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ToolsContentControl == null)
            return;
            
        if (ToolsListBox.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            UpdateToolContent(tag);
        }
    }

    private void UpdateToolContent(string toolName)
    {
        if (ToolsContentControl == null)
            return;
            
        ToolsContentControl.Content = toolName switch
        {
            "Ping" => CreatePingPanel(),
            "NetworkCalculator" => CreateNetworkCalculatorPanel(),
            _ => CreateDraftPanel(toolName)
        };
    }

    private Control CreatePingPanel()
    {
        return new PingView();
    }

    private Control CreateNetworkCalculatorPanel()
    {
        return new NetworkCalculatorView();
    }

    private StackPanel CreateDraftPanel(string toolName)
    {
        var toolDisplayName = toolName switch
        {
            "Ping" => "PING",
            "Wlan" => "WLAN",
            "NetworkCalculator" => "Netzwerk-Rechner",
            "NetworkScanner" => "Netzwerkscanner",
            "LogViewer" => "Log Viewer",
            _ => toolName
        };

        return new StackPanel
        {
            Spacing = 24,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock 
                { 
                    Text = "🚧", 
                    FontSize = 64,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock 
                { 
                    Text = $"{toolDisplayName}", 
                    FontSize = 28, 
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock 
                { 
                    Text = "Draft - In Planung", 
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.Orange
                },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(24, 16),
                    Margin = new Thickness(0, 16, 0, 0),
                    MaxWidth = 500,
                    Child = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock 
                            { 
                                Text = "ℹ️ Hinweis",
                                FontSize = 16,
                                FontWeight = FontWeight.SemiBold
                            },
                            new TextBlock 
                            { 
                                Text = "Diese Funktion befindet sich noch in der Planungsphase und wird in einer zukünftigen Version implementiert.",
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            }
        };
    }
}
