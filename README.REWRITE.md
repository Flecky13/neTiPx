# neTiPx - Cross-Platform Network Tools

**Cross-Platform Rewrite mit Avalonia UI** 🚀

## ⚠️ Branch Status

Dieser Branch (`rewrite`) enthält die neue **Cross-Platform Version** von neTiPx mit Avalonia UI.

- ✅ **Windows** (x64, ARM64)
- ✅ **Linux** (x64, ARM64)
- ✅ **macOS** (x64, ARM64 / Apple Silicon)

Das alte WinUI3-Projekt befindet sich in `src/neTiPx.UI.WinUI.Legacy/`.

---

## 📁 Projektstruktur

```
neTiPx-CrossPlatform/
├── src/
│   ├── neTiPx.Core/                    # ⚙️ Business Logic (plattformunabhängig)
│   │   ├── Models/                     # Datenmodelle
│   │   ├── Services/                   # Service-Implementierungen
│   │   ├── Interfaces/                 # Service-Interfaces
│   │   └── Helpers/                    # Helper-Klassen
│   │
│   ├── neTiPx.UI.Avalonia/             # 🖥️ Cross-Platform UI
│   │   ├── Views/                      # XAML Views
│   │   ├── ViewModels/                 # ViewModels (MVVM)
│   │   ├── Converters/                 # Value Converters
│   │   ├── Assets/                     # Icons, Images
│   │   └── Resources/                  # Styles, Themes
│   │
│   ├── neTiPx.Services.Windows/        # 🪟 Windows-spezifische Services
│   ├── neTiPx.Services.Linux/          # 🐧 Linux-spezifische Services
│   ├── neTiPx.Services.macOS/          # 🍎 macOS-spezifische Services
│   │
│   └── neTiPx.UI.WinUI.Legacy/         # 🗄️ Altes WinUI3 Projekt (Legacy)
│
├── .github/workflows/                  # CI/CD GitHub Actions
├── AVALONIA_MIGRATION_PLAN.md          # Detaillierter Migrationsplan
└── neTiPx-CrossPlatform.sln            # Solution-Datei
```

---

## 🚀 Entwicklung

### Voraussetzungen

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Windows:** Visual Studio 2022 oder VS Code
- **Linux:** VS Code mit C# Extension
- **macOS:** VS Code oder Visual Studio for Mac

### Build & Run

```bash
# Dependencies installieren
dotnet restore neTiPx-CrossPlatform.sln

# Debug Build
dotnet build neTiPx-CrossPlatform.sln --configuration Debug

# Release Build
dotnet build neTiPx-CrossPlatform.sln --configuration Release

# Ausführen (plattformabhängig)
dotnet run --project src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj
```

### Plattformspezifisch Publishen

```bash
# Windows x64
dotnet publish src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj -c Release -r linux-x64 --self-contained

# macOS ARM64 (Apple Silicon)
dotnet publish src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj -c Release -r osx-arm64 --self-contained
```

---

## 🏗️ Architektur

### Service-Abstraktionen

Plattformspezifische Funktionen sind durch Interfaces abstrahiert:

| Interface | Windows | Linux | macOS |
|-----------|---------|-------|-------|
| `IWifiNetworkService` | wlanapi.dll | nmcli | airport |
| `ITrayService` | Shell32 | AppIndicator | NSMenu |
| `IAutoStartService` | Registry | .desktop | LaunchAgent |
| `INetworkConfigService` | netsh | ip/nmcli | networksetup |
| `IProcessService` | Process.Start | bash | zsh |
| `IFileDialogService` | Win32 | GTK | Cocoa |

### Dependency Injection

```csharp
// In App.axaml.cs
services.AddCoreServices();          // Plattformunabhängig
services.AddWindowsServices();       // Nur auf Windows
services.AddLinuxServices();         // Nur auf Linux
services.AddMacOSServices();         // Nur auf macOS
```

---

## 🎯 Features

### Implementiert ✅

- [x] Projektstruktur & Architektur
- [x] Core Models & Service-Interfaces
- [x] Dependency Injection Setup
- [x] Avalonia UI Basis-Framework
- [x] GitHub Actions CI/CD (Windows, Linux, macOS)
- [x] Cross-Platform Build

### In Entwicklung 🚧

- [ ] WiFi-Scanning (Windows: wlanapi, Linux: nmcli, macOS: airport)
- [ ] Tray-Service (Windows: Shell32, Linux: AppIndicator, macOS: NSMenu)
- [ ] IP-Profil-Verwaltung
- [ ] Routen-Management
- [ ] UNC-Pfad-Verwaltung (Windows)
- [ ] Netzwerk-Scanner
- [ ] Settings & Konfiguration
- [ ] Sprach-Unterstützung
- [ ] Theme-Switching (Light/Dark/System)

### Geplant 📋

- [ ] Installer-Pakete (MSIX, AppImage, DMG)
- [ ] Automatische Updates
- [ ] Log-Viewer
- [ ] Ping-Monitoring
- [ ] Port-Scanner

---

## 📦 CI/CD & Releases

GitHub Actions baut automatisch für alle Plattformen:

- **Windows:** x64, ARM64
- **Linux:** x64, ARM64
- **macOS:** x64, ARM64 (Apple Silicon)

Builds werden als Artifacts hochgeladen und können heruntergeladen werden.

---

## 🛠️ Für Entwickler

### Neue Features hinzufügen

1. **Service-Interface** in `neTiPx.Core/Interfaces/` erstellen
2. **Plattformspezifische Implementierungen** in `neTiPx.Services.{Platform}/`
3. **Service registrieren** in `ServiceCollectionExtensions.cs`
4. **UI in Avalonia** implementieren (`Views/`, `ViewModels/`)

### Neue plattformspezifische Funktion

```csharp
// 1. Interface definieren (Core)
public interface IMyService
{
    Task DoSomethingAsync();
}

// 2. Windows-Implementierung
public class MyServiceWindows : IMyService
{
    [DllImport("kernel32.dll")]
    private static extern void WindowsSpecificAPI();
    
    public async Task DoSomethingAsync()
    {
        WindowsSpecificAPI();
    }
}

// 3. Linux-Implementierung
public class MyServiceLinux : IMyService
{
    public async Task DoSomethingAsync()
    {
        await Process.Start("linux-command");
    }
}

// 4. Service registrieren
services.AddSingleton<IMyService, MyServiceWindows>();  // Windows
services.AddSingleton<IMyService, MyServiceLinux>();    // Linux
```

---

## 📝 Lizenz

MIT License - siehe [LICENSE](LICENSE) Datei

---

## 🤝 Beitragen

Contributions sind willkommen! Bitte:

1. Fork das Repository
2. Erstelle einen Feature-Branch (`git checkout -b feature/AmazingFeature`)
3. Committe deine Änderungen (`git commit -m 'Add some AmazingFeature'`)
4. Push zum Branch (`git push origin feature/AmazingFeature`)
5. Öffne einen Pull Request

---

## 📧 Support

Bei Fragen oder Problemen bitte ein Issue erstellen.

---

**Status:** 🚧 In aktiver Entwicklung (Branch: `rewrite`)
