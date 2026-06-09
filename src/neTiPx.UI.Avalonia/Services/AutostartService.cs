using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace neTiPx.UI.Avalonia.Services;

/// <summary>
/// Service zur Verwaltung des Autostarts der Anwendung beim Systemstart.
/// Unterstützt Windows, Linux und macOS.
/// </summary>
public sealed class AutostartService
{
    private const string AppName = "neTiPx";
    
    /// <summary>
    /// Prüft, ob Autostart aktiviert ist.
    /// </summary>
    public bool IsAutostartEnabled()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return IsAutostartEnabledWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return IsAutostartEnabledLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return IsAutostartEnabledMacOS();
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Aktiviert oder deaktiviert den Autostart.
    /// </summary>
    public bool SetAutostart(bool enabled)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return SetAutostartWindows(enabled);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return SetAutostartLinux(enabled);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return SetAutostartMacOS(enabled);
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    #region Windows
    
    [SupportedOSPlatform("windows")]
    private bool IsAutostartEnabledWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        return key?.GetValue(AppName) != null;
    }
    
    [SupportedOSPlatform("windows")]
    private bool SetAutostartWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null)
            return false;
            
        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath))
                return false;
                
            // Mit --minimized Parameter starten
            key.SetValue(AppName, $"\"{exePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
        
        return true;
    }
    
    #endregion
    
    #region Linux
    
    private bool IsAutostartEnabledLinux()
    {
        var autostartFile = GetLinuxAutostartFilePath();
        return File.Exists(autostartFile);
    }
    
    private bool SetAutostartLinux(bool enabled)
    {
        var autostartFile = GetLinuxAutostartFilePath();
        var autostartDir = Path.GetDirectoryName(autostartFile);
        
        if (enabled)
        {
            if (!Directory.Exists(autostartDir))
            {
                Directory.CreateDirectory(autostartDir!);
            }
            
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath))
                return false;
            
            var desktopEntry = $@"[Desktop Entry]
Type=Application
Version=1.0
Name=neTiPx
Comment=Network Tools and IP Configuration Manager
Exec=""{exePath}"" --minimized
Icon=netipx
Terminal=false
Categories=Network;System;
StartupNotify=false
X-GNOME-Autostart-enabled=true
";
            
            File.WriteAllText(autostartFile, desktopEntry);
            
            // Datei ausführbar machen
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    var chmod = System.Diagnostics.Process.Start("chmod", $"+x \"{autostartFile}\"");
                    chmod?.WaitForExit();
                }
                catch { }
            }
        }
        else
        {
            if (File.Exists(autostartFile))
            {
                File.Delete(autostartFile);
            }
        }
        
        return true;
    }
    
    private string GetLinuxAutostartFilePath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome))
        {
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        
        return Path.Combine(configHome, "autostart", $"{AppName}.desktop");
    }
    
    #endregion
    
    #region macOS
    
    private bool IsAutostartEnabledMacOS()
    {
        var launchAgentFile = GetMacOSLaunchAgentPath();
        return File.Exists(launchAgentFile);
    }
    
    private bool SetAutostartMacOS(bool enabled)
    {
        var launchAgentFile = GetMacOSLaunchAgentPath();
        var launchAgentDir = Path.GetDirectoryName(launchAgentFile);
        
        if (enabled)
        {
            if (!Directory.Exists(launchAgentDir))
            {
                Directory.CreateDirectory(launchAgentDir!);
            }
            
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exePath))
                return false;
            
            var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.netipx.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
        <string>--minimized</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>
";
            
            File.WriteAllText(launchAgentFile, plistContent);
        }
        else
        {
            if (File.Exists(launchAgentFile))
            {
                File.Delete(launchAgentFile);
            }
        }
        
        return true;
    }
    
    private string GetMacOSLaunchAgentPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, "Library", "LaunchAgents", $"com.{AppName.ToLower()}.plist");
    }
    
    #endregion
}
