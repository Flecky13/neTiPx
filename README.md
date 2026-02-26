# neTiPx

**neTiPx** ist ein modernes WinUI 3 Desktop-Tool für Windows zur Netzwerkverwaltung und -konfiguration.

## Features

- 🔌 **Adapter-Verwaltung**: Auswahl und Konfiguration von Netzwerkadaptern
- 🌐 **IP-Konfiguration**: DHCP oder manuelle IP-Einstellungen
- 📊 **Netzwerk-Informationen**: Übersicht über aktuelle Netzwerkverbindungen
- 🛠️ **Tools**: Ping-Tests und WLAN-Scanner
- 🎨 **Theme-Support**: Hell/Dunkel/System-Theme
- 📍 **System Tray**: Icon mit Mouseover-Anzeige für schnelle Netzwerk-Infos

## Systemanforderungen

- **Windows 10** Version 1809 (Build 17763) oder höher
- **.NET 8.0 Runtime**
- **WinUI 3** (Windows App SDK)

## Build

```powershell
# Projekt bauen
dotnet build neTiPx.sln -c Release

# Für spezifische Platform
dotnet build neTiPx.sln -c Release /p:Platform=x64
```

## Konfiguration

Die Anwendung speichert Einstellungen in `%LOCALAPPDATA%\config.ini`:

- Adapter-Auswahl (Adapter1, Adapter2)
- IP-Profile mit Namen
- Theme-Einstellungen
- Ping-Adressen
