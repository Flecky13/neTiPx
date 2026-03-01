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
  - [Einstellungen](#einstellungen)
- [Funktionen im Detail](#-funktionen-im-detail)
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

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 🔧 Funktionen im Detail

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
- **UI-Framework**: WinUI 3 (Windows App SDK)
- **Berechtigungen**: Administrator-Rechte für Änderungen an Netzwerkeinstellungen

---

## 📦 Installation

### Installation

1. Laden Sie das neueste Setup-Paket aus dem [Releases](../../releases)-Bereich herunter
2. Führen Sie `neTiPx_Setup_Vx.x.x.x.exe` aus
3. Folgen Sie den Anweisungen des Installationsassistenten
4. Starten Sie neTiPx über das Startmenü oder Desktop-Icon


**Hinweis**: Für Änderungen an Netzwerkeinstellungen sind Administrator-Rechte erforderlich.

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
---

## 📄 Lizenz & Kontakt

Siehe `LICENSE` im Repository. Für Fragen zum Code bitte Issues/PRs im Repo verwenden.

https://buymeacoffee.com/pedrotepe

zurück zum
[Inhaltsverzeichnis]#-Inhaltsverzeichnis
