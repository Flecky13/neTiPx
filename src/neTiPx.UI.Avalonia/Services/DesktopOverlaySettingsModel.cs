using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace neTiPx.UI.Avalonia.Services;

public static class DesktopOverlayInfoKeys
{
    public const string ComputerName = "computerName";
    public const string User = "user";
    public const string IPv4 = "ipv4";
    public const string IPv6 = "ipv6";
    public const string Gateway = "gateway";
    public const string ExternalIp = "externalIp";
    public const string Domain = "domain";
    public const string OperatingSystem = "operatingSystem";
    public const string Uptime = "uptime";
    public const string RamUsage = "ramUsage";
    public const string NetworkAdapter = "networkAdapter";
    public const string ComputerModel = "computerModel";
    public const string ComputerManufacturer = "computerManufacturer";
    public const string OperatingSystemName = "operatingSystemName";
    public const string OperatingSystemVersion = "operatingSystemVersion";
    public const string DiskFreeSpace = "diskFreeSpace";
    public const string DiskUsedSpace = "diskUsedSpace";
    public const string CpuModel = "cpuModel";
    public const string ProcessorCount = "processorCount";
    public const string FreeText = "freeText";

    public static readonly string[] DefaultOrder =
    {
        ComputerName,
        User,
        IPv4,
        IPv6,
        Gateway,
        ExternalIp,
        Domain,
        OperatingSystem,
        Uptime,
        RamUsage,
        NetworkAdapter
    };

    public static readonly string[] AvailableKeys = DefaultOrder
        .Concat(new[]
        {
            ComputerModel,
            ComputerManufacturer,
            OperatingSystemName,
            OperatingSystemVersion,
            DiskFreeSpace,
            DiskUsedSpace,
            CpuModel,
            ProcessorCount,
            FreeText
        })
        .ToArray();

    public static bool IsKnown(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && AvailableKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
}

public static class DesktopOverlayPositionModes
{
    public const string TopLeft = "TopLeft";
    public const string TopRight = "TopRight";
    public const string BottomLeft = "BottomLeft";
    public const string BottomRight = "BottomRight";
    public const string Free = "Free";

    public static string Normalize(string? mode)
    {
        if (string.Equals(mode, TopLeft, StringComparison.OrdinalIgnoreCase)) return TopLeft;
        if (string.Equals(mode, TopRight, StringComparison.OrdinalIgnoreCase)) return TopRight;
        if (string.Equals(mode, BottomLeft, StringComparison.OrdinalIgnoreCase)) return BottomLeft;
        if (string.Equals(mode, Free, StringComparison.OrdinalIgnoreCase)) return Free;
        return BottomRight;
    }
}

public sealed class DesktopOverlayItemSetting
{
    public string Key { get; set; } = string.Empty;
    public bool ShowLabel { get; set; } = true;
    public string CustomText { get; set; } = string.Empty;
    public int Order { get; set; }

    // Used only while reading settings created before the list-based UI.
    // A hidden legacy entry maps naturally to an entry removed from the list.
    [JsonPropertyName("isVisible")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyIsVisible { get; set; }
}

public sealed class DesktopOverlaySettingsModel
{
    public bool Enabled { get; set; }
    public string PositionMode { get; set; } = DesktopOverlayPositionModes.BottomRight;
    public int OffsetX { get; set; } = 24;
    public int OffsetY { get; set; } = 24;
    public int FreeX { get; set; } = 80;
    public int FreeY { get; set; } = 80;
    public int Width { get; set; } = 360;
    public int Height { get; set; } = 360;

    public double FontSize { get; set; } = 14;
    public string FontFamily { get; set; } = "Consolas";
    public string TextColor { get; set; } = "#FFFFFFFF";
    public string BackgroundColor { get; set; } = "#1A1A1A";
    public int BackgroundOpacityPercent { get; set; } = 65;
    public int Padding { get; set; } = 12;
    public int LineSpacing { get; set; } = 4;
    public int CornerRadius { get; set; } = 10;
    public bool ShowShadow { get; set; } = true;
    public bool HoverInteractive { get; set; }

    public int NetworkRefreshSeconds { get; set; } = 5;
    public int ExternalIpRefreshMinutes { get; set; } = 5;
    public int RamRefreshSeconds { get; set; } = 1;
    public int UptimeRefreshSeconds { get; set; } = 60;

    public List<DesktopOverlayItemSetting> Items { get; set; } = new();

    public static DesktopOverlaySettingsModel CreateDefault()
    {
        var model = new DesktopOverlaySettingsModel
        {
            Enabled = false
        };

        for (var i = 0; i < DesktopOverlayInfoKeys.DefaultOrder.Length; i++)
        {
            model.Items.Add(new DesktopOverlayItemSetting
            {
                Key = DesktopOverlayInfoKeys.DefaultOrder[i],
                ShowLabel = true,
                Order = i
            });
        }

        return model;
    }

    public static DesktopOverlaySettingsModel Normalize(DesktopOverlaySettingsModel? model)
    {
        var normalized = model ?? CreateDefault();

        normalized.PositionMode = DesktopOverlayPositionModes.Normalize(normalized.PositionMode);
        normalized.OffsetX = Math.Max(0, normalized.OffsetX);
        normalized.OffsetY = Math.Max(0, normalized.OffsetY);
        normalized.FreeX = Math.Max(0, normalized.FreeX);
        normalized.FreeY = Math.Max(0, normalized.FreeY);
        normalized.Width = Math.Clamp(normalized.Width, 180, 1200);
        normalized.Height = Math.Clamp(normalized.Height, 120, 2000);
        normalized.FontSize = Math.Clamp(normalized.FontSize, 9, 36);
        normalized.FontFamily = string.IsNullOrWhiteSpace(normalized.FontFamily) ? "Consolas" : normalized.FontFamily.Trim();
        normalized.TextColor = string.IsNullOrWhiteSpace(normalized.TextColor) ? "#FFFFFFFF" : normalized.TextColor.Trim();
        normalized.BackgroundColor = string.IsNullOrWhiteSpace(normalized.BackgroundColor) ? "#1A1A1A" : normalized.BackgroundColor.Trim();
        normalized.BackgroundOpacityPercent = Math.Clamp(normalized.BackgroundOpacityPercent, 0, 100);
        normalized.Padding = Math.Clamp(normalized.Padding, 0, 64);
        normalized.LineSpacing = Math.Clamp(normalized.LineSpacing, 0, 32);
        normalized.CornerRadius = Math.Clamp(normalized.CornerRadius, 0, 64);
        normalized.NetworkRefreshSeconds = Math.Clamp(normalized.NetworkRefreshSeconds, 1, 3600);
        normalized.ExternalIpRefreshMinutes = Math.Clamp(normalized.ExternalIpRefreshMinutes, 1, 1440);
        normalized.RamRefreshSeconds = Math.Clamp(normalized.RamRefreshSeconds, 1, 3600);
        normalized.UptimeRefreshSeconds = Math.Clamp(normalized.UptimeRefreshSeconds, 1, 3600);

        // The default list is used only for a new configuration.  Once the user
        // has edited the list, omitted entries must remain omitted.
        normalized.Items = (normalized.Items ?? new List<DesktopOverlayItemSetting>())
            .Where(item => item != null
                && item.LegacyIsVisible != false
                && DesktopOverlayInfoKeys.IsKnown(item.Key))
            .OrderBy(item => item.Order)
            .Take(20)
            .Select((item, index) => new DesktopOverlayItemSetting
            {
                Key = DesktopOverlayInfoKeys.AvailableKeys.First(key =>
                    key.Equals(item.Key, StringComparison.OrdinalIgnoreCase)),
                ShowLabel = item.ShowLabel,
                CustomText = (item.CustomText ?? string.Empty).Trim()[..Math.Min((item.CustomText ?? string.Empty).Trim().Length, 32)],
                Order = index
            })
            .ToList();

        return normalized;
    }
}

public static class DesktopOverlaySettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(DesktopOverlaySettingsModel? model)
    {
        var normalized = DesktopOverlaySettingsModel.Normalize(model);
        return JsonSerializer.Serialize(normalized, Options);
    }

    public static DesktopOverlaySettingsModel Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DesktopOverlaySettingsModel.CreateDefault();
        }

        try
        {
            var model = JsonSerializer.Deserialize<DesktopOverlaySettingsModel>(json, Options);
            return DesktopOverlaySettingsModel.Normalize(model);
        }
        catch
        {
            return DesktopOverlaySettingsModel.CreateDefault();
        }
    }
}
