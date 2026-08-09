# neTiPx

🌍 Language: Deutsch

Thisway for [English](README.md)

<p align="center">
  <img src="Bilder/toolicon.png" alt="neTiPx Logo" width="120"/>
</p>

**neTiPx** ist ein modernes Desktop-Tool für Windows zur komfortablen Verwaltung von Netzwerkadaptern und IP-Konfigurationen. Mit einer intuitiven Benutzeroberfläche bietet neTiPx schnellen Zugriff auf alle wichtigen Netzwerkeinstellungen und -informationen.

> Hinweis: Die letzte reine Windows-Version ist 1.7.2.0.
>
> Die erste Multiplattform-Version ist 2.0.4.7.
>
> Aktueller Teststatus:

> - Linux: getestet
> - Windows: getestet
> - macOS: sollte funktionieren, ist aktuell aber ungetestet
>
> Falls bei einem Update von Version 1.7.2.0 auf 2.x Probleme auftreten, bitte die alte Version deinstallieren und die neue Version sauber neu installieren.

---

## 📋 Inhaltsverzeichnis

- [Features](#-features)
- [Screenshots](#-screenshots)
  - [Adapter-Übersicht](#adapter-übersicht)
  - [IP-Konfiguration](#ip-konfiguration)
  - [Ping Tool](#ping-tool)
  - [Netzwerk-Rechner](#netzwerk-rechner)
  - [Routen Tool](#routen-tool)
  - [Info](#info)
  - [Einstellungen](#einstellungen)
- [Funktionen im Detail](#-funktionen-im-detail)
  - [PING Tool](#ping-tool-1)
  - [Routenverwaltung und Routing-Analyse](#routenverwaltung-und-routing-analyse)
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
- 📝 (DRAFT) **Ping-Logging**: Automatische Log-Dateien pro Ziel inklusive Öffnen, Exportieren und Löschen
- 🧭 **Hintergrundbetrieb**: Pings laufen optional weiter, wenn die Ping-Seite nicht aktiv ist
- 📡 (DRAFT) **WLAN Scanner**: Native Windows API für detaillierte WLAN-Netzwerk-Informationen
- 🧮 **Netzwerk-Rechner**: IP-Subnetz-Berechnungen mit intelligenter Bereichserkennung und bidirektionaler Synchronisierung
- 🔎 (DRAFT) **Netzwerkscanner**: Scan von IP-Bereichen mit Port-Prüfung und Detailansicht gefundener Geräte
- 📄 (DRAFT) **Log Viewer**: Öffnen und Live-Anzeigen von Logdateien mit Filter, Highlight-Regeln, 16-Farben-Swatch-Auswahl und optionalem Auto-Scroll
- 🛣️ **Routen Tool**: Anzeige aktueller IPv4-Routen inkl. Löschfunktion für benutzerseitige/persistente Routen und direktem Hinzufügen neuer Routen
- 🧩 **Modulare Tools-Seite**: Ping, WLAN, Netzwerk-Rechner, Netzwerkscanner, Log Viewer und Routen als eigene Unterseiten mit Lazy-Loading
- 🗂️ (DRAFT) **Seiten-Sichtbarkeit**: Haupt- und Toolseiten können über `PagesVisibility.xml` ein-/ausgeblendet werden
- 🛠️ (DRAFT) **Versteckte Admin-Konfiguration**: Auf der Settings-Seite öffnet das Wort `Wünschen` einen Dialog zur Pflege der Seiten-Sichtbarkeit

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
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
- **Routen pro Profil**: Verwalten Sie statische IPv4-Routen direkt im IP-Profil
- **Routenmodus**: Wählen Sie pro Profil zwischen `ersetzen` und `hinzufügen` vorhandener persistenter Routen
- **Systemabgleich**: Bereits vorhandene Systemrouten werden beim Profil-Dialog erkannt und markiert
- **Echtzeit-Verbindungsstatus**: Überwachen Sie Gateway und DNS-Server mit farbcodierter Ampel
  - 🟢 Grün: Erreichbar (guter Ping)
  - 🟡 Gelb: Erreichbar (langsamer Ping)
  - 🔴 Rot: Nicht erreichbar
- **Ping-Anzeige**: Zeigt aktuelle Ping-Zeiten für Gateway und DNS-Server

### Ping Tool

Das Ping Tool ermöglicht die Überwachung mehrerer Ziele mit eigener Taktung und Protokollanzeige:

![Ping Tool](Bilder/tools_ping.png)

**Funktionen:**

- **Mehrere Ziele**: IPs oder Hostnamen hinzufügen und parallel überwachen
- **Intervall pro Ziel**: Eigene Ping-Frequenz je Eintrag
- **IPv4/IPv6 Anzeige**: Antwortzeit und Status-Ampel pro Protokoll
- **Aktiv-Status pro Zeile**: Einzelne Ziele unabhängig ein- und ausschalten
- **Hintergrund-Option**: Pings laufen optional weiter, auch wenn die Ping-Seite nicht im Fokus ist
- **Status für nicht genutzte Protokolle**: Anzeige `inaktiv` mit grauer Ampel

### Netzwerk-Rechner

Der Netzwerk-Rechner bietet intelligente IP-Subnetz-Berechnungen mit automatischer Synchronisierung:

![Netzwerk-Rechner](Bilder/tools_NetCalc.png)

**Funktionen:**

- **Intelligente Eingabe**: IP-Adresse, Subnetzmaske oder CIDR-Sufix - alle Felder aktualisieren sich automatisch
- **Bidirektionale Synchronisierung**:
  - Änderung der Subnetzmaske → automatische Berechnung von CIDR-Sufix und Max. Hosts
  - Änderung des CIDR-Sufix → automatische Berechnung von Subnetzmaske und Max. Hosts
  - Änderung von Max. Hosts → automatische Berechnung von Subnetzmaske und CIDR-Sufix
- **Plus/Minus-Steuerung**: Schnelles Umschalten zwischen gültigen Host-Anzahlen (z.B. 254 → 510 → 1022)
- **Automatische Berechnung**: Ergebnisse werden sofort bei gültigen Eingaben angezeigt
- **IP-Bereichserkennung**: Automatische Klassifizierung der eingegebenen IP:
  - Privater Bereich (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
  - Public Bereich
  - Loopback (127.x.x.x)
  - Zeroconf/Link-Local (169.254.x.x)
  - Multicast (224.x.x.x - 239.x.x.x)
  - Shared Address Space/CGNAT (100.64.x.x)
  - Dokumentationsbereich
  - Broadcast, Unspecified, Reserviert
- **Detaillierte Ergebnisse**:
  - Netzwerkadresse und Broadcast-Adresse
  - Erste und letzte verwendbare IP
  - Subnetzmaske und CIDR-Sufix
  - Anzahl verfügbarer Hosts
  - Wildcard-Maske

### Routen Tool

Das Routen Tool zeigt die aktuelle IPv4-Routing-Tabelle und unterstützt die gezielte Analyse für ein konkretes Ziel.

![Routen Tool](Bilder/tools_routen.png)

### Info

Die Info-Seite bündelt Versions- und Update-Informationen sowie wichtige Links.

![Info](Bilder/Infos.png)

**Funktionen:**

- **Routenübersicht**: Anzeige aktueller und persistenter IPv4-Routen inklusive Default-Route (`0.0.0.0/0`)
- **Löschlogik nach Quelle**: Löschbutton nur für benutzerseitige/statische Routen, Systemrouten werden als `Systemroute` gekennzeichnet
- **Ziel-IP-Filter**: Eingabe einer Ziel-IP zeigt nur die tatsächlich relevanten Routen (Longest Prefix Match + Metrik)
- **Sortierbare Tabelle**: Sortierung über Spaltenköpfe mit Richtungsanzeige (`▲`/`▼`)
- **Route hinzufügen**: Persistente Route direkt aus dem Tool anlegen

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

#### 🎨 Farbthemen

- **Theme-Auswahl**: Wählen Sie aus mehreren vordefinierten Farbthemen
  - Hell/Dunkel/System
  - Rot, Blau, Grün, Orange, Lila, Türkis
- **Benutzerdefinierte Themes**: Erstellen und bearbeiten Sie eigene Farbthemen
- **Theme-Editor**: Passen Sie Hintergrund-, Text- und Akzentfarben individuell an

#### 🌐 Sprachauswahl

- Die Anwendung unterstützt mehrere Sprachen. Über das Dropdown-Menü in den Einstellungen kann die Anzeigesprache gewählt werden.
- Im Dropdown werden die Eigenbezeichnungen der Sprachen (z. B. „Deutsch", „English", „Español") angezeigt. Diese werden dynamisch aus den Sprachdateien geladen.
- Änderungen der Sprache wirken sich sofort auf die gesamte Benutzeroberfläche aus.

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
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

### Routenverwaltung und Routing-Analyse

- **Quellenbasierte Klassifizierung**: Kombination aus `route print`, CIM (`Win32_IP4PersistedRouteTable`) und `Get-NetRoute` zur Unterscheidung von System- und Benutzer-Routen
- **Persistenz-Erkennung**: Statische/persistente Routen werden als löschbar erkannt, systemseitige On-link/Local/DHCP-Routen bleiben geschützt
- **Routing-Entscheidung im Filter**: Für Ziel-IPs werden nur Kandidaten mit bestem Präfix und bester Metrik angezeigt
- **Sichere Lösch-/Add-Operationen**: Route-Änderungen erfolgen erhöht und werden nach Aktion in der Tabelle neu eingelesen

### IP-Profilverwaltung

- **Mehrere Profile**: Speichern Sie unterschiedliche Netzwerkkonfigurationen für verschiedene Standorte (Büro, Home Office, Extern)
- **Schnelles Umschalten**: Wechseln Sie mit wenigen Klicks zwischen gespeicherten Profilen
- **DHCP-Unterstützung**: Automatische IP-Konfiguration via DHCP
- **Manuelle Konfiguration**: Detaillierte Kontrolle über IP-Adressen, Subnetzmasken, Gateway und DNS
- **Integrierte Routenverwaltung**: Profilbezogene statische IPv4-Routen mit Dialog zur Pflege und Systemabgleich
- **Validierung**: Automatische Überprüfung der eingegebenen IP-Adressen und Netzwerkkonfiguration
- **Multi-IP**: Weisen Sie einem Adapter mehrere IP-Adressen gleichzeitig zu

### Theme-System

- **Anpassbare Oberfläche**: Passen Sie das Aussehen der Anwendung an Ihre Vorlieben an
- **Vordefinierte Themes**: Mehrere professionelle Farbschemata zur Auswahl
- **Echtzeit-Vorschau**: Sehen Sie Änderungen sofort in der Anwendung

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
---

## 💻 Systemanforderungen

- **Betriebssystem**: Windows, Linux oder macOS
- **Framework**: .NET 8.0 Runtime
- **UI-Framework**: Avalonia UI
- **Berechtigungen**: Administrator-Rechte für Änderungen an Netzwerkeinstellungen

---

## 📦 Installation

### Installation (Windows)

1. **Systemanforderungen prüfen**: Stellen Sie sicher, dass die .NET 8 Runtime installiert ist (siehe [Systemanforderungen](#-systemanforderungen))
2. Laden Sie das neueste Setup-Paket aus dem [Releases](../../releases)-Bereich herunter
3. Führen Sie `neTiPx_Setup_Vx.x.x.x.exe` aus
4. Folgen Sie den Anweisungen des Installationsassistenten
5. Starten Sie neTiPx über das Startmenü oder Desktop-Icon

**Hinweise**:

- Für Änderungen an Netzwerkeinstellungen sind Administrator-Rechte erforderlich.
- Wenn beim Start eine Fehlermeldung bezüglich des Windows App SDK angezeigt wird, siehe [Systemanforderungen](#windows-app-sdk).

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
---

## �️ Build & Entwicklung

Für die Erstellung von Builds für Windows, Linux und macOS siehe die Build-Dokumentation:

- **[Installation/BUILD_QUICKSTART.md](Installation/BUILD_QUICKSTART.md)** - Schnellstart für alle Plattformen
- **[Installation/BUILD_AND_DEPLOY.md](Installation/BUILD_AND_DEPLOY.md)** - Detaillierte Build- und Deployment-Anleitung

**Unterstützte Plattformen:**

- Windows (x64, x86, ARM64)
- Linux (x64, ARM64) - mit .deb, AppImage und tar.gz
- macOS (Intel, Apple Silicon) - mit .app Bundle und .dmg

**Schnellstart:**

```bash
# Alle Plattformen bauen
./Installation/build-all.sh        # Linux/macOS
.\Installation\build-all.ps1       # Windows

# Plattformspezifisch mit Paketen
./Installation/build-linux.sh      # Linux (.deb, AppImage)
./Installation/build-macos.sh      # macOS (.app, .dmg)
.\Installation\build-windows.ps1   # Windows (mit NSIS)
```

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
---

## �📄 Lizenz & Kontakt

Siehe `LICENSE` im Repository. Für Fragen zum Code bitte Issues/PRs im Repo verwenden.

https://buymeacoffee.com/pedrotepe

zurück zum
[Inhaltsverzeichnis](#-inhaltsverzeichnis)
