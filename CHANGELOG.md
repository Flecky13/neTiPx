# Changelog neTiPx

Alle wichtigen Änderungen dieses Projekts werden in dieser Datei dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.1.6.1]

### Added
- Application-Icon in der EXE eingebettet (Taskleisten-Icon sichtbar)
- Latest-Version-Anzeige mit Prüfzeitpunkt: Vx.x.x.x - zuletzt geprüft am DD.MM.YYYY HH:MM
- Hilfe-Button auf Info-Seite (öffnet README.md auf GitHub)

### Changed
- Letzte erfolgreiche Update-Prüfung (Version + Zeitstempel) wird in User_Settings.xml gespeichert und beim Start geladen

## [1.1.6.0]

### Added
- Automatischer Download und Installation von Updates
- Setup.exe wird von GitHub Release heruntergeladen und direkt installiert

### Fixed
- Vollständige Versionsanzeige auf der Info Page (x.x.x.x statt x.x.x)

## [1.1.5.0]

### Added
- Lizenz & Kontakt Sektion auf Info Page
- Support-Links (GitHub Profil, Buy Me a Coffee)

### Fixed
- XML-Parsing Fehler in InfoPage.xaml (&amp; Encoding)
- Grid.Spacing durch ColumnSpacing/RowSpacing ersetzt

## [1.1.4.0]

### Added
- Erweiterte GitHub Release-Automation mit Changelog-Integration
- Vorschau-System für Release Notes
- Automatische Changelog-Extraktion

### Changed
- GitHub Release Batch-Skript mit verbesserter Fehlerbehandlung


## [1.1.3.0]

### Added
- Update-Prüfung gegen GitHub API
- Automatische Versionsermittlung aus csproj
- GitHub Release Batch-Skript mit Changelog-Integration
- Info Page mit Update-Status Anzeige

### Fixed
- FileVersion in neTiPx.exe korrekt gesetzt
- NSIS Skript verwendet korrekten Publish-Pfad
- Layout-Anpassung auf IpConfigPage

## [1.1.2.0]

### Added
- System Tray Integration
- Hover-Fenster für schnelle Netzwerk-Infos
- Ping-Tools für Gateway und DNS-Monitoring
- Farbtheme-System

### Fixed
- XmlSerializer Exception-Handling

## [1.1.1.0]

### Added
- IP-Profilmanager
- DHCP und Manual Mode Support
- Validierung von Netzwerkeinstellungen

## [1.1.0.0]

### Initial Release
- Adapter-Verwaltung
- Netzwerk-Informationen
- Grundlegende UI mit WinUI 3
