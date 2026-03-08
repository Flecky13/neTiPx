# neTiPx

<p align="center">
  <img src="Bilder/toolicon.png" alt="neTiPx Logo" width="120"/>
</p>

**neTiPx** ist ein modernes Desktop-Tool für Windows zur komfortablen Verwaltung von Netzwerkadaptern und IP-Konfigurationen. Mit einer intuitiven Benutzeroberfläche bietet neTiPx schnellen Zugriff auf alle wichtigen Netzwerkeinstellungen und -informationen.

---

## 📋 Inhaltsverzeichnis

- [Features](#-features)
- [Screenshots](#-screenshots)
  - [Adapter-Übersicht](#adapter-übersicht)
  - [IP-Konfiguration](#ip-konfiguration)
  - [Ping Tool](#ping-tool)
  - [Einstellungen](#einstellungen)
- [Funktionen im Detail](#-funktionen-im-detail)
  - [PING Tool](#ping-tool-1)
  - [Ping-Logging](#ping-logging)
- [Systemanforderungen](#-systemanforderungen)
- [Installation](#-installation)

---

## ✨ Features

- 🔌 **Adapter-Verwaltung**: Übersicht über bis zu zwei Netzwerkadapter mit detaillierten Informationen
- 🌐 **IP-Profilmanager**: Verwaltung mehrerer IP-Profile für schnelles Umschalten zwischen Netzwerkkonfigurationen
- 📊 **Netzwerk-Informationen**: Detaillierte Anzeige von IPv4/IPv6-Adressen, Gateway, DNS und MAC-Adressen
- 🎯 **Verbindungsstatus**: Echtzeit-Ping-Überwachung von Gateway und DNS-Servern mit visueller Ampel
- 🎨 **Theme-Support**: Anpassbare Farbthemen (Hell/Dunkel/System) mit mehreren vordefinierten Farbschemata
- 📍 **System Tray Integration**: Minimierung in die Taskleiste mit Hover-Fenster für schnelle Netzwerk-Infos
- 🚀 **Autostart**: Optional beim Systemstart starten
- 🛰️ **PING Tool**: Mehrere Ziele parallel überwachen (IPv4/IPv6), pro Ziel aktivierbar/deaktivierbar
- 📝 **Ping-Logging**: Automatische Log-Dateien pro Ziel inklusive Öffnen, Exportieren und Löschen
- 🧭 **Hintergrundbetrieb**: Pings laufen optional weiter, wenn die Ping-Seite nicht aktiv ist
- 📡 **WLAN Scanner**: Native Windows API für detaillierte WLAN-Netzwerk-Informationen

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 📸 Screenshots

### Adapter-Übersicht

Die Adapter-Seite zeigt detaillierte Informationen zu Ihren konfigurierten Netzwerkadaptern:

![Adapter-Übersicht](Bilder/Adapter_Page.png)

**Angezegte Informationen:**
- Name und MAC-Adresse des Adapters
- IPv4-Adressen mit Subnetzmasken
- IPv6-Adressen
- Gateway-Adressen (IPv4 und IPv6)
- DNS-Server (IPv4 und IPv6)
- Übersichtliche Darstellung für bis zu zwei Adapter gleichzeitig


### IP-Konfiguration

Verwalten Sie mehrere IP-Profile und wechseln Sie schnell zwischen verschiedenen Netzwerkkonfigurationen:

![IP-Konfiguration](Bilder/IP_Konfigurations_Page.png)

**Funktionen:**
- **Profilmanager**: Erstellen, bearbeiten und löschen Sie IP-Profile
- **DHCP oder Manuell**: Wählen Sie zwischen automatischer und manueller IP-Konfiguration
- **Multiple IP-Adressen**: Weisen Sie einem Adapter mehrere IP-Adressen zu
- **DNS-Konfiguration**: Konfigurieren Sie primäre und sekundäre DNS-Server
- **Echtzeit-Verbindungsstatus**: Überwachen Sie Gateway und DNS-Server mit farbcodierter Ampel
  - 🟢 Grün: Erreichbar (guter Ping)
  - 🟡 Gelb: Erreichbar (langsamer Ping)
  - 🔴 Rot: Nicht erreichbar
- **Ping-Anzeige**: Zeigt aktuelle Ping-Zeiten für Gateway und DNS-Server

### Ping Tool

Das Ping Tool ermöglicht die Überwachung mehrerer Ziele mit eigener Taktung und Protokollanzeige:

![Ping Tool](Bilder/Tool_Page.png)

**Funktionen:**
- **Mehrere Ziele**: IPs oder Hostnamen hinzufügen und parallel überwachen
- **Intervall pro Ziel**: Eigene Ping-Frequenz je Eintrag
- **IPv4/IPv6 Anzeige**: Antwortzeit und Status-Ampel pro Protokoll
- **Aktiv-Status pro Zeile**: Einzelne Ziele unabhängig ein- und ausschalten
- **Hintergrund-Option**: Pings laufen optional weiter, auch wenn die Ping-Seite nicht im Fokus ist
- **Status für nicht genutzte Protokolle**: Anzeige `inaktiv` mit grauer Ampel

### WLAN Scanner

Der WLAN Scanner nutzt die native Windows WLAN API für detaillierte Netzwerkinformationen:

**Funktionen:**
- **Native API**: Direkter Zugriff auf Windows WLAN-Schnittstelle
- **Sortierbare Tabelle**: Klicken Sie auf Spaltenüberschriften zum Sortieren
  - 📶 Signal-Symbol (Stärke-Visualisierung)
  - SSID (Netzwerkname)
  - Signal (Prozent)
  - BSSID (MAC-Adresse des Access Points)
- **Detaillierte Informationen** in drei Bereichen:
  - **Signal**: Stärke (%), Qualität (%), RSSI (dBm)
  - **Frequenz**: Band (2.4G/5G/6G), Kanal, Frequenz (MHz)
  - **Sicherheit & Standard**: Verschlüsselung (🔓 gesichert / 🔒 offen), PHY-Typ (802.11a/b/g/n/ac/ax), Netzwerk-Typ
- **Band-Erkennung**: Automatische Erkennung von 2.4 GHz, 5 GHz und 6 GHz (Wi-Fi 6E)
- **Signal-Symbole**:
  - 📶 Stark (≥75%)
  - 📳 Mittel (50-74%)
  - 📴 Schwach (25-49%)
  - ❌ Sehr schwach (<25%)

### Einstellungen

Konfigurieren Sie die Anwendung nach Ihren Bedürfnissen:

![Einstellungen](Bilder/Einstellungen_Page.png)

**Einstellungsmöglichkeiten:**

#### 📡 Netzwerkadapter
- **Adapter 1 & 2**: Wählen Sie die zwei Hauptadapter aus, die auf der Adapter-Seite angezeigt werden
- Nur aktive Netzwerkadapter werden zur Auswahl angezeigt

#### 🔔 System Tray
- **Hover-Fenster**: Zeigt Netzwerkinformationen beim Überfahren des Tray-Icons
- **Minimierung**: Option zum Minimieren in die Taskleiste statt Schließen

#### 🚀 Autostart
- **Bei Windows-Start**: Startet die Anwendung automatisch beim Systemstart
- **Minimiert starten**: Startet die Anwendung minimiert im System Tray

#### 📝 Ping-Logging
- **Log-Ordner wählen**: Eigener Speicherort für Ping-Logs auswählbar
- **Standard-Ordner**: Schnell auf den Standardpfad zurücksetzen
- **Pfadanzeige**: Dynamisch angepasste Ein-Zeilen-Anzeige mit Tooltip für den vollständigen Pfad

#### 🎨 Farbthemen
- **Theme-Auswahl**: Wählen Sie aus mehreren vordefinierten Farbthemen
  - Hell/Dunkel/System
  - Rot, Blau, Grün, Orange, Lila, Türkis
- **Benutzerdefinierte Themes**: Erstellen und bearbeiten Sie eigene Farbthemen
- **Theme-Editor**: Passen Sie Hintergrund-, Text- und Akzentfarben individuell an

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 🔧 Funktionen im Detail

### PING Tool

- **Paralleles Monitoring**: Mehrere Ziele werden gleichzeitig überwacht
- **Zieltypen**: Unterstützt IPv4, IPv6 und Hostnamen
- **Sichtbares Protokollverhalten**:
  - Nicht verwendetes Protokoll zeigt `inaktiv` und eine graue Ampel
  - Deaktiviertes Ziel zeigt `Deaktiviert` für beide Protokolle
- **Flexible Aktivierung**:
  - Pro Ziel über die Zeilen-Checkbox
  - Global für Hintergrundbetrieb über `im Hintergrund weiter aktiv`

### Ping-Logging

- **Pro Ziel eigene Log-Datei**: Eindeutige Dateinamen, auch bei Sonderzeichen im Zielnamen
- **CSV-Format mit Zeitstempel**: `Zeit;Ziel;Protokoll;Antwortzeit`
- **Direkte Aktionen in der Liste**:
  - Log-Datei öffnen
  - Beim Löschen wahlweise mitlöschen
  - Vor dem Löschen optional per `Speichern unter` exportieren
- **Protokollspezifisches Logging**: Nur relevante IPv4/IPv6-Einträge werden geschrieben

### WLAN Scanner - Technische Details

- **Native Windows WLAN API**: Direkter P/Invoke-Zugriff auf wlanapi.dll
  - WlanOpenHandle: Initialisierung der WLAN-Schnittstelle
  - WlanEnumInterfaces: Auflistung verfügbarer WLAN-Adapter
  - WlanGetNetworkBssList: Abruf detaillierter BSS-Informationen
- **Thread-sichere UI-Updates**: DispatcherQueue für sichere Updates aus Background-Threads
- **Umfassende Netzwerkinformationen**:
  - Signal: dBm, Prozent, Link-Qualität
  - Frequenz: MHz, Kanal, Band (2.4/5/6 GHz)
  - Sicherheit: Privacy Bit, Verschlüsselungsstatus
  - Standard: PHY-Typ (802.11-Varianten), Netzwerk-Typ (Infrastructure/Ad-Hoc)
  - Hardware: BSSID, Beacon-Intervall
- **Robustheit**: Automatischer Fallback auf netsh-Kommandozeile bei API-Problemen

### IP-Profilverwaltung

- **Mehrere Profile**: Speichern Sie unterschiedliche Netzwerkkonfigurationen für verschiedene Standorte (Büro, Home Office, Extern)
- **Schnelles Umschalten**: Wechseln Sie mit wenigen Klicks zwischen gespeicherten Profilen
- **DHCP-Unterstützung**: Automatische IP-Konfiguration via DHCP
- **Manuelle Konfiguration**: Detaillierte Kontrolle über IP-Adressen, Subnetzmasken, Gateway und DNS
- **Validierung**: Automatische Überprüfung der eingegebenen IP-Adressen und Netzwerkkonfiguration
- **Multi-IP**: Weisen Sie einem Adapter mehrere IP-Adressen gleichzeitig zu

### Verbindungsqualität

- **Automatische Überwachung**: Kontinuierliches Pingen von Gateway und DNS-Servern (alle 5 Sekunden)
- **Visuelle Anzeige**: Farbcodierte Ampel zeigt den Status auf einen Blick
- **Ping-Zeiten**: Detaillierte Anzeige der Antwortzeiten in Millisekunden
- **Mehrfach-Überwachung**: Gleichzeitige Überwachung von Gateway, DNS1 und DNS2

### Theme-System

- **Anpassbare Oberfläche**: Passen Sie das Aussehen der Anwendung an Ihre Vorlieben an
- **Vordefinierte Themes**: Mehrere professionelle Farbschemata zur Auswahl
- **Echtzeit-Vorschau**: Sehen Sie Änderungen sofort in der Anwendung

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 💻 Systemanforderungen

- **Betriebssystem**: Windows 10 Version 1809 (Build 17763) oder höher
- **Framework**: .NET 8.0 Runtime
- **UI-Framework**: WinUI 3 (Windows App SDK) - **erforderlich**
- **Berechtigungen**: Administrator-Rechte für Änderungen an Netzwerkeinstellungen

### Windows App SDK

neTiPx erfordert das **Windows App SDK 1.8.5** zur Ausführung. Wenn Sie den folgenden Fehler erhalten:

![Fehlendes Windows App SDK](Bilder/FehlendeMSIX.png)

Laden Sie das Windows App SDK herunter und installieren Sie es von:
[Microsoft Windows App SDK Downloads](https://docs.microsoft.com/windows/apps/windows-app-sdk/downloads)

---

## 📦 Installation

### Installation

1. **Systemanforderungen prüfen**: Stellen Sie sicher, dass das Windows App SDK installiert ist (siehe [Systemanforderungen](#-systemanforderungen))
2. Laden Sie das neueste Setup-Paket aus dem [Releases](../../releases)-Bereich herunter
3. Führen Sie `neTiPx_Setup_Vx.x.x.x.exe` aus
4. Folgen Sie den Anweisungen des Installationsassistenten
5. Starten Sie neTiPx über das Startmenü oder Desktop-Icon

**Hinweise**:
- Für Änderungen an Netzwerkeinstellungen sind Administrator-Rechte erforderlich.
- Wenn beim Start eine Fehlermeldung bezüglich des Windows App SDK angezeigt wird, siehe [Systemanforderungen](#windows-app-sdk).

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 📄 Lizenz & Kontakt

Siehe `LICENSE` im Repository. Für Fragen zum Code bitte Issues/PRs im Repo verwenden.

https://buymeacoffee.com/pedrotepe

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
