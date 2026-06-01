using System;
using System.IO;

namespace neTiPx.Core.Helpers;

/// <summary>
/// Provides paths to configuration files for neTiPx.
/// All configuration files are stored in the user's ApplicationData folder.
/// </summary>
public static class ConfigFileHelper
{
    private static string GetConfigDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "neTiPx");
        
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        return dir;
    }

    /// <summary>
    /// Gets the path to the legacy config.ini file.
    /// </summary>
    public static string GetConfigIniPath()
    {
        try
        {
            var dir = GetConfigDirectory();
            var target = Path.Combine(dir, "config.ini");

            // Migration: Copy from application directory if exists
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
                // Ignore migration errors
            }

            return target;
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        }
    }

    /// <summary>
    /// Gets the path to the IP profiles XML file.
    /// </summary>
    public static string GetIpProfilesXmlPath()
    {
        try
        {
            var dir = GetConfigDirectory();
            var target = Path.Combine(dir, "IP-Profile.xml");
            var old = Path.Combine(dir, "ip-profiles.xml");

            // Migration: Rename old file if exists
            try
            {
                if (!File.Exists(target) && File.Exists(old))
                {
                    File.Copy(old, target, false);
                }
            }
            catch
            {
                // Ignore migration errors
            }

            return target;
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP-Profile.xml");
        }
    }

    /// <summary>
    /// Gets the path to the network adapters configuration XML file.
    /// </summary>
    public static string GetAdaptersXmlPath()
    {
        try
        {
            var dir = GetConfigDirectory();
            return Path.Combine(dir, "Adapter.xml");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Adapter.xml");
        }
    }

    /// <summary>
    /// Gets the path to the user settings XML file.
    /// </summary>
    public static string GetUserSettingsXmlPath()
    {
        try
        {
            var dir = GetConfigDirectory();
            return Path.Combine(dir, "User_Settings.xml");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "User_Settings.xml");
        }
    }

    /// <summary>
    /// Gets the path to the ping targets XML file.
    /// </summary>
    public static string GetPingTargetsXmlPath()
    {
        try
        {
            var dir = GetConfigDirectory();
            return Path.Combine(dir, "PingTargets.xml");
        }
        catch
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PingTargets.xml");
        }
    }
}
