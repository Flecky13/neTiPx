using System;
using System.IO;

namespace neTiPx.WinUI.Helpers
{
    public static class ConfigFileHelper
    {
        public static string GetConfigIniPath()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "neTiPx");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var target = Path.Combine(dir, "config.ini");

                var old = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                try
                {
                    if (!File.Exists(target) && File.Exists(old))
                    {
                        File.Copy(old, target, false);
                    }
                }
                catch
                {
                }

                return target;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            }
        }

        public static string GetIpProfilesXmlPath()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "neTiPx");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var target = Path.Combine(dir, "IP-Profile.xml");
                var old = Path.Combine(dir, "ip-profiles.xml");

                try
                {
                    if (!File.Exists(target) && File.Exists(old))
                    {
                        File.Copy(old, target, false);
                    }
                }
                catch
                {
                }

                return target;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP-Profile.xml");
            }
        }
    }
}
