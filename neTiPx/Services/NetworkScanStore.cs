using System;
using System.IO;
using System.Xml.Linq;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class NetworkScanStore
    {
        public string ReadLastScanRanges()
        {
            var path = ConfigFileHelper.GetNetworkScanRangesXmlPath();
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return string.Empty;
                }

                var rangesElement = root.Element("ranges");
                return rangesElement?.Value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void WriteLastScanRanges(string ranges)
        {
            if (string.IsNullOrWhiteSpace(ranges))
            {
                return;
            }

            try
            {
                var root = new XElement("networkScan",
                    new XElement("ranges", ranges.Trim())
                );

                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
                var path = ConfigFileHelper.GetNetworkScanRangesXmlPath();
                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                doc.Save(path);
            }
            catch
            {
                // Fehler beim Speichern ignorieren
            }
        }
    }
}
