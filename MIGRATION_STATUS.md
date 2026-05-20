# neTiPx Migration Status - WinUI → Avalonia

**Migrations-Dokumentation für Cross-Platform Rewrite**

---

## 📊 Migrations-Fortschritt

### Phase 1: Foundation ✅ ABGESCHLOSSEN

- [x] Projektstruktur erstellt (`src/neTiPx.Core`, `src/neTiPx.UI.Avalonia`, etc.)
- [x] .NET 8.0 SDK Konfiguration
- [x] Solution-Datei erstellt (`neTiPx-CrossPlatform.sln`)
- [x] GitHub Actions CI/CD Setup
- [x] Erfolgreicher Cross-Platform Build (Windows, Linux, macOS)

### Phase 2: Core Abstractions ✅ ABGESCHLOSSEN

- [x] Service-Interfaces definiert:
  - `IWifiNetworkService`
  - `ITrayService`
  - `IAutoStartService`
  - `INetworkConfigService`
  - `IFileDialogService`
  - `IProcessService`
- [x] Dependency Injection Setup
- [x] Plattformspezifische Extension-Methoden

### Phase 3: Models Migration ✅ ABGESCHLOSSEN

- [x] `IpProfile` (mit CommunityToolkit.Mvvm)
- [x] `RouteEntry`
- [x] `IpAddressEntry`
- [x] `NetworkDevice`
- [x] `UncPathProfile` / `UncPathEntry`
- [x] `PingTarget`
- [x] Common Types (Enums, Records)

### Phase 4: UI Foundation ✅ ABGESCHLOSSEN

- [x] Avalonia Basis-Setup
- [x] `Program.cs` mit Plattform-Detection
- [x] `App.axaml` & `App.axaml.cs`
- [x] `MainWindow.axaml` mit Tab-Navigation
- [x] Theme-Ressourcen (Fluent Design)
- [x] Assets & Language Files kopiert

### Phase 5: Service Implementations 🚧 IN ARBEIT

#### Windows Services
- [ ] `WifiNetworkServiceWindows` (wlanapi.dll P/Invoke)
- [ ] `TrayServiceWindows` (Shell32, User32)
- [ ] `AutoStartServiceWindows` (Registry)
- [ ] `NetworkConfigServiceWindows` (netsh)
- [ ] `ProcessServiceWindows`
- [ ] `FileDialogServiceWindows`

#### Linux Services
- [ ] `WifiNetworkServiceLinux` (nmcli)
- [ ] `TrayServiceLinux` (AppIndicator/DBus)
- [ ] `AutoStartServiceLinux` (.desktop file)
- [ ] `NetworkConfigServiceLinux` (ip/nmcli)
- [ ] `ProcessServiceLinux` (bash)
- [ ] `FileDialogServiceLinux` (GTK)

#### macOS Services
- [ ] `WifiNetworkServiceMacOS` (airport)
- [ ] `TrayServiceMacOS` (NSMenu)
- [ ] `AutoStartServiceMacOS` (LaunchAgent)
- [ ] `NetworkConfigServiceMacOS` (networksetup)
- [ ] `ProcessServiceMacOS` (zsh)
- [ ] `FileDialogServiceMacOS` (Cocoa)

### Phase 6: Views & ViewModels 🔜 GEPLANT

- [ ] IP Configuration View
- [ ] Network Scanner View
- [ ] UNC Paths View
- [ ] Settings View
- [ ] Log Viewer
- [ ] Converter Migration (WinUI → Avalonia)

### Phase 7: Testing & Polishing 🔜 GEPLANT

- [ ] Unit Tests für Services
- [ ] Integration Tests
- [ ] UI-Testing
- [ ] Performance-Optimierung
- [ ] Dokumentation

### Phase 8: Deployment 🔜 GEPLANT

- [ ] Windows: MSIX Package
- [ ] Linux: AppImage, .deb, .rpm
- [ ] macOS: .dmg, Homebrew Cask
- [ ] Auto-Update Mechanismus

---

## 🔄 Code-Migration-Status

### Von WinUI3 nach Avalonia umgestellt

| Komponente | Status | Notizen |
|-----------|--------|---------|
| **XAML Syntax** | ✅ Basis | Namespaces angepasst |
| **Data Binding** | ✅ Basis | `x:Bind` → `{Binding}` |
| **Styles** | ✅ Basis | Fluent Theme aktiviert |
| **Navigation** | ✅ Basis | TabControl (vorläufig) |
| **ObservableObject** | ✅ | CommunityToolkit.Mvvm |
| **RelayCommand** | ✅ | CommunityToolkit.Mvvm |

### Noch zu migrieren

| Komponente | Priorität | Schwierigkeit |
|-----------|-----------|--------------|
| **WifiNetworks.cs** | 🔴 Hoch | Hoch (P/Invoke) |
| **TrayService.cs** | 🔴 Hoch | Sehr hoch |
| **NetworkConfigService.cs** | 🟡 Mittel | Mittel |
| **IniFile.cs** | 🟢 Niedrig | Niedrig |
| **Views/** | 🔴 Hoch | Mittel |
| **ViewModels/** | 🟡 Mittel | Niedrig |
| **Converters/** | 🟢 Niedrig | Niedrig |

---

## 🛠️ Aktuelle Arbeits-Aufgaben

### Nächste Schritte

1. **Windows WiFi Service implementieren**
   - Bestehenden Code von `WifiNetworks.cs` übernehmen
   - In `WifiNetworkServiceWindows` einkapseln
   - Interface `IWifiNetworkService` implementieren

2. **Linux WiFi Service implementieren**
   - `nmcli device wifi list` Parsing
   - `nmcli connection up` für Connect
   - Error-Handling

3. **TrayService für Windows**
   - Shell32/User32 P/Invoke
   - Custom WndProc
   - Context-Menu

4. **IP Profile View migrieren**
   - XAML von WinUI → Avalonia
   - ViewModel anpassen
   - Bindings testen

---

## 📝 Wichtige Änderungen

### Neue Konzepte

- **Plattform-Abstraktionen:** Alle OS-spezifischen Features hinter Interfaces
- **Dependency Injection:** ServiceCollection statt statische Services
- **Conditional Compilation:** `#if WINDOWS / LINUX / OSX`
- **MVVM mit CommunityToolkit:** Statt eigener `ObservableObject`

### Breaking Changes

- `ObservableObject` → `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`
- Properties jetzt mit `[ObservableProperty]` Attribut
- Commands mit `[RelayCommand]`
- Namespace-Änderungen: `neTiPx.Core.Models`, `neTiPx.Core.Interfaces`

---

## 🐛 Bekannte Probleme

### Build-Probleme

- ✅ **GELÖST:** Windows-Projekt kann nicht unter Linux gebaut werden
  - Lösung: `EnableWindowsTargeting=true` + Conditional References

- ✅ **GELÖST:** Compilation Symbols für plattformspezifische Code
  - Lösung: `<DefineConstants>` in .csproj

### Runtime-Probleme

- ⚠️ **OFFEN:** Services noch nicht implementiert (Dummy-Implementierungen)
- ⚠️ **OFFEN:** Assets fehlen teilweise

---

## 📦 Deployment-Status

### GitHub Actions

- ✅ Windows Build (x64, ARM64)
- ✅ Linux Build (x64, ARM64)
- ✅ macOS Build (x64, ARM64)
- ✅ Artifact Upload
- ⏳ Release Creation (bei Tags)

### Installer

- ⏳ Windows MSIX
- ⏳ Linux AppImage
- ⏳ macOS DMG

---

## 🎯 Meilensteine

| Meilenstein | Ziel-Datum | Status |
|------------|-----------|--------|
| Foundation | ✅ Abgeschlossen | ✅ |
| Core Services | TBD | 🚧 30% |
| Windows Services | TBD | 🔜 0% |
| Linux Services | TBD | 🔜 0% |
| macOS Services | TBD | 🔜 0% |
| UI Migration | TBD | 🔜 5% |
| Beta Release | TBD | 🔜 0% |
| Stable Release | TBD | 🔜 0% |

---

**Letztes Update:** 20. Mai 2026

**Migrations-Lead:** [Ihr Name]

**Status:** 🟢 Aktiv in Entwicklung
