using System;
using System.IO;
using System.Xml.Linq;

namespace neTiPx.UI.Avalonia.Services;

public sealed class PageVisibilityStore
{
    public sealed class Config
    {
        // Adapters, Info, Settings are always visible – not stored here.
        public bool ShowIpConfig   { get; set; } = true;
        public bool ShowRoutes     { get; set; } = true;
        public bool ShowUncPath    { get; set; } = true;
        public bool ShowTools      { get; set; } = true;

        public bool ShowToolNetCalc   { get; set; } = true;
        public bool ShowToolPing      { get; set; } = true;
        public bool ShowToolWlan      { get; set; } = true;
        public bool ShowToolNetScan   { get; set; } = true;
        public bool ShowToolLogViewer { get; set; } = true;
    }

    public Config Read()
    {
        var path = GetPath();
        if (!File.Exists(path)) return new Config();
        try
        {
            var root = XDocument.Load(path).Root;
            if (root == null) return new Config();
            bool B(string name, bool def)
            {
                var v = (string?)root.Attribute(name);
                return v is null ? def : v.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return new Config
            {
                ShowIpConfig      = B("showIpConfig",      true),
                ShowRoutes        = B("showRoutes",        true),
                ShowUncPath       = B("showUncPath",       true),
                ShowTools         = B("showTools",         true),
                ShowToolNetCalc   = B("showToolNetCalc",   true),
                ShowToolPing      = B("showToolPing",      true),
                ShowToolWlan      = B("showToolWlan",      true),
                ShowToolNetScan   = B("showToolNetScan",   true),
                ShowToolLogViewer = B("showToolLogViewer", true),
            };
        }
        catch { return new Config(); }
    }

    public void Write(Config config)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("pagesVisibility",
                new XAttribute("showIpConfig",      config.ShowIpConfig),
                new XAttribute("showRoutes",        config.ShowRoutes),
                new XAttribute("showUncPath",       config.ShowUncPath),
                new XAttribute("showTools",         config.ShowTools),
                new XAttribute("showToolNetCalc",   config.ShowToolNetCalc),
                new XAttribute("showToolPing",      config.ShowToolPing),
                new XAttribute("showToolWlan",      config.ShowToolWlan),
                new XAttribute("showToolNetScan",   config.ShowToolNetScan),
                new XAttribute("showToolLogViewer", config.ShowToolLogViewer)));
        doc.Save(GetPath());
    }

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neTiPx");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, "PagesVisibility.xml");
    }
}
