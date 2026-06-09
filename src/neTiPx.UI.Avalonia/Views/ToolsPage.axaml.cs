using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using neTiPx.UI.Avalonia.Views.Tools;

namespace neTiPx.UI.Avalonia.Views;

public partial class ToolsPage : UserControl
{
    public ToolsPage()
    {
        InitializeComponent();
        
        // Set initial content after initialization
        if (ToolsListBox.SelectedIndex == 0)
        {
            UpdateToolContent("Ping");
        }
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
            "NetworkCalculator" => CreateNetworkCalculatorPanel(),
            _ => CreateDraftPanel(toolName)
        };
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
