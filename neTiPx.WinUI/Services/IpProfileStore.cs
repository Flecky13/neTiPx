using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Models;

namespace neTiPx.WinUI.Services
{
    public sealed class IpProfileStore
    {
        private readonly ConfigStore _configStore = new ConfigStore();

        public List<IpProfile> ReadAllProfiles()
        {
            var path = ConfigFileHelper.GetIpProfilesXmlPath();
            if (File.Exists(path))
            {
                return ReadFromXml(path);
            }

            var migratedProfiles = ReadFromLegacyIni();
            if (migratedProfiles.Count > 0)
            {
                WriteAllProfiles(migratedProfiles);
            }

            return migratedProfiles;
        }

        public bool TryGetProfile(string profileName, out IpProfile profile)
        {
            var profiles = ReadAllProfiles();
            var found = profiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (found == null)
            {
                profile = new IpProfile { Name = profileName };
                return false;
            }

            profile = found;
            return true;
        }

        public void SaveProfile(IpProfile profile)
        {
            var profiles = ReadAllProfiles();
            var index = profiles.FindIndex(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            var clone = CloneProfile(profile);

            if (index >= 0)
            {
                profiles[index] = clone;
            }
            else
            {
                profiles.Add(clone);
            }

            WriteAllProfiles(profiles);
        }

        public void RemoveProfile(string profileName)
        {
            var profiles = ReadAllProfiles();
            profiles = profiles
                .Where(p => !string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            WriteAllProfiles(profiles);
        }

        public bool HasPersistedProfileSettings(string profileName)
        {
            if (!TryGetProfile(profileName, out var profile))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(profile.Gateway) ||
                !string.IsNullOrWhiteSpace(profile.Dns1) ||
                !string.IsNullOrWhiteSpace(profile.Dns2))
            {
                return true;
            }

            return profile.IpAddresses.Any(entry => !string.IsNullOrWhiteSpace(entry.IpAddress));
        }

        private static List<IpProfile> ReadFromXml(string path)
        {
            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return new List<IpProfile>();
                }

                var profiles = new List<IpProfile>();
                foreach (var profileElement in root.Elements("profile"))
                {
                    var adapterNameAttr = (string?)profileElement.Attribute("adapterName");
                    var profile = new IpProfile
                    {
                        Name = (string?)profileElement.Attribute("name") ?? "IP #1",
                        AdapterName = string.IsNullOrWhiteSpace(adapterNameAttr) ? null : adapterNameAttr,
                        Mode = NormalizeMode((string?)profileElement.Attribute("mode") ?? "DHCP"),
                        Gateway = (string?)profileElement.Attribute("gateway") ?? string.Empty,
                        Dns1 = (string?)profileElement.Attribute("dns1") ?? string.Empty,
                        Dns2 = (string?)profileElement.Attribute("dns2") ?? string.Empty,
                        IsDirty = false
                    };

                    var ipEntries = profileElement.Element("ipAddresses")?.Elements("entry") ?? Enumerable.Empty<XElement>();
                    foreach (var entry in ipEntries)
                    {
                        profile.IpAddresses.Add(new IpAddressEntry
                        {
                            IpAddress = (string?)entry.Attribute("ipAddress") ?? string.Empty,
                            SubnetMask = (string?)entry.Attribute("subnetMask") ?? string.Empty
                        });
                    }

                    profiles.Add(profile);
                }

                return profiles;
            }
            catch
            {
                return new List<IpProfile>();
            }
        }

        private static void WriteAllProfiles(IEnumerable<IpProfile> profiles)
        {
            var root = new XElement("ipProfiles",
                profiles.Select(profile =>
                    new XElement("profile",
                        new XAttribute("name", profile.Name ?? string.Empty),
                        new XAttribute("adapterName", profile.AdapterName ?? string.Empty),
                        new XAttribute("mode", NormalizeMode(profile.Mode)),
                        new XAttribute("gateway", profile.Gateway ?? string.Empty),
                        new XAttribute("dns1", profile.Dns1 ?? string.Empty),
                        new XAttribute("dns2", profile.Dns2 ?? string.Empty),
                        new XElement("ipAddresses",
                            profile.IpAddresses
                                .Where(entry => !string.IsNullOrWhiteSpace(entry.IpAddress))
                                .Select(entry =>
                                    new XElement("entry",
                                        new XAttribute("ipAddress", entry.IpAddress ?? string.Empty),
                                        new XAttribute("subnetMask", entry.SubnetMask ?? string.Empty)))))));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            var path = ConfigFileHelper.GetIpProfilesXmlPath();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            doc.Save(path);
        }

        private List<IpProfile> ReadFromLegacyIni()
        {
            var values = _configStore.ReadAll();
            var names = GetProfileNames(values);
            var profiles = new List<IpProfile>();

            foreach (var name in names)
            {
                profiles.Add(ReadLegacyProfile(values, name));
            }

            return profiles;
        }

        private static List<string> GetProfileNames(Dictionary<string, string> values)
        {
            if (values.TryGetValue("IpProfileNames", out var names) && !string.IsNullOrWhiteSpace(names))
            {
                return names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (values.Keys.Any(k => k.StartsWith("IpTab1", StringComparison.OrdinalIgnoreCase)))
            {
                return new List<string> { "IP #1" };
            }

            return new List<string>();
        }

        private static IpProfile ReadLegacyProfile(Dictionary<string, string> values, string name)
        {
            var profile = new IpProfile { Name = name };

            if (values.TryGetValue($"{name}.Adapter", out var adapter))
            {
                profile.AdapterName = adapter;
            }
            else if (values.TryGetValue("Adapter1", out var adapter1))
            {
                profile.AdapterName = adapter1;
            }
            else if (values.TryGetValue("IpTab1Adapter", out var legacyAdapter))
            {
                profile.AdapterName = legacyAdapter;
            }

            if (values.TryGetValue($"{name}.Mode", out var mode))
            {
                profile.Mode = NormalizeMode(mode);
            }
            else if (values.TryGetValue("IpTab1Mode", out var legacyMode))
            {
                profile.Mode = NormalizeMode(legacyMode);
            }

            if (values.TryGetValue($"{name}.GW", out var gw))
            {
                profile.Gateway = gw;
            }
            else if (values.TryGetValue("IpTab1GW", out var legacyGw))
            {
                profile.Gateway = legacyGw;
            }

            if (values.TryGetValue($"{name}.DNS1", out var dns1))
            {
                profile.Dns1 = dns1;
            }

            if (values.TryGetValue($"{name}.DNS2", out var dns2))
            {
                profile.Dns2 = dns2;
            }

            if (string.IsNullOrWhiteSpace(profile.Dns1) && string.IsNullOrWhiteSpace(profile.Dns2))
            {
                if (values.TryGetValue($"{name}.DNS", out var dns))
                {
                    profile.Dns = dns;
                }
                else if (values.TryGetValue("IpTab1DNS", out var legacyDns))
                {
                    profile.Dns = legacyDns;
                }
            }

            var entries = ReadLegacyIpEntries(values, name);
            foreach (var entry in entries)
            {
                profile.IpAddresses.Add(entry);
            }

            return profile;
        }

        private static List<IpAddressEntry> ReadLegacyIpEntries(Dictionary<string, string> values, string name)
        {
            var entries = new List<IpAddressEntry>();

            for (int i = 1; i <= 10; i++)
            {
                if (!values.TryGetValue($"{name}.IP_{i}", out var ip))
                {
                    break;
                }

                values.TryGetValue($"{name}.Subnet_{i}", out var subnet);
                entries.Add(new IpAddressEntry
                {
                    IpAddress = ip ?? string.Empty,
                    SubnetMask = subnet ?? string.Empty
                });
            }

            if (entries.Count == 0 && values.TryGetValue("IpTab1IP", out var legacyIp))
            {
                values.TryGetValue("IpTab1Subnet", out var legacySubnet);
                entries.Add(new IpAddressEntry
                {
                    IpAddress = legacyIp ?? string.Empty,
                    SubnetMask = legacySubnet ?? string.Empty
                });
            }

            return entries;
        }

        private static string NormalizeMode(string mode)
        {
            if (mode.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            {
                return "Manual";
            }

            return "DHCP";
        }

        private static IpProfile CloneProfile(IpProfile source)
        {
            var clone = new IpProfile
            {
                Name = source.Name,
                AdapterName = source.AdapterName,
                Mode = NormalizeMode(source.Mode),
                Gateway = source.Gateway,
                Dns1 = source.Dns1,
                Dns2 = source.Dns2,
                IsDirty = false
            };

            foreach (var entry in source.IpAddresses)
            {
                clone.IpAddresses.Add(new IpAddressEntry
                {
                    IpAddress = entry.IpAddress,
                    SubnetMask = entry.SubnetMask
                });
            }

            return clone;
        }
    }
}
