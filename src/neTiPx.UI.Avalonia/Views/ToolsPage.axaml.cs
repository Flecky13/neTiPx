using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

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
            "Ping" => CreatePingPanel(),
            "Wlan" => CreateWlanPanel(),
            "NetworkCalculator" => CreateNetworkCalculatorPanel(),
            "NetworkScanner" => CreateNetworkScannerPanel(),
            "Routes" => CreateRoutesPanel(),
            "LogViewer" => CreateLogViewerPanel(),
            "UncPath" => CreateUncPathPanel(),
            _ => CreatePingPanel()
        };
    }

    private StackPanel CreatePingPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "PING Tool", FontSize = 20, FontWeight = FontWeight.Bold },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Host/IP-Adresse:", FontWeight = FontWeight.SemiBold },
                        new TextBox { Watermark = "z.B. google.com oder 8.8.8.8" }
                    }
                },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Anzahl:", FontWeight = FontWeight.SemiBold },
                        new NumericUpDown { Value = 4, Minimum = 1, Maximum = 100 }
                    }
                },
                new Button { Content = "Ping starten", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 200,
                    Child = new TextBlock { Text = "Ping-Ergebnisse werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateWlanPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "WLAN Tool", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "WLAN-Netzwerke scannen und verwalten" },
                new Button { Content = "Netzwerke scannen", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 200,
                    Child = new TextBlock { Text = "Gefundene WLAN-Netzwerke werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateNetworkCalculatorPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Netzwerk-Rechner", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "IP-Adressen und Subnetze berechnen" },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "IP-Adresse:", FontWeight = FontWeight.SemiBold },
                        new TextBox { Watermark = "z.B. 192.168.1.0" }
                    }
                },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "CIDR / Subnet Mask:", FontWeight = FontWeight.SemiBold },
                        new TextBox { Watermark = "z.B. 24 oder 255.255.255.0" }
                    }
                },
                new Button { Content = "Berechnen", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 150,
                    Child = new TextBlock { Text = "Berechnungsergebnisse werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateNetworkScannerPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Netzwerkscanner", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Scannen Sie Ihr Netzwerk nach aktiven Geräten" },
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "IP-Bereich:", FontWeight = FontWeight.SemiBold },
                        new TextBox { Watermark = "z.B. 192.168.1.0/24" }
                    }
                },
                new Button { Content = "Scan starten", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 200,
                    Child = new TextBlock { Text = "Gefundene Geräte werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateRoutesPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Routen", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Netzwerkrouten anzeigen und verwalten" },
                new Button { Content = "Routen aktualisieren", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 200,
                    Child = new TextBlock { Text = "Routing-Tabelle wird hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateLogViewerPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "Log Viewer", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Anwendungslogs anzeigen" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new Button { Content = "Logs aktualisieren", Classes = { "accent" } },
                        new Button { Content = "Logs löschen" }
                    }
                },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 300,
                    Child = new TextBlock { Text = "Log-Einträge werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }

    private StackPanel CreateUncPathPanel()
    {
        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = "UNC Pfade", FontSize = 20, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "Netzwerkpfade verwalten und verbinden" },
                new Button { Content = "Neuer UNC-Pfad", Classes = { "accent" } },
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    MinHeight = 200,
                    Child = new TextBlock { Text = "Gespeicherte UNC-Pfade werden hier angezeigt...", Foreground = Brushes.Gray }
                }
            }
        };
    }
}
