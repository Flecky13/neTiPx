# Changelog neTiPx

Alle wichtigen Änderungen dieses Projekts werden in dieser Datei dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
## [1.6.3.0]

### Changed
- **Einstellungen / Adapter**: Die zweite Netzwerkkarte (`NIC 2`) kann in den Einstellungen jetzt optional leer bleiben (`Keine Auswahl`).
- **Lokalisierung / Einstellungen**: Neuer Sprachschlüssel für die optionale Auswahl der zweiten Netzwerkkarte in allen Dateien unter `lang` synchronisiert und übersetzt.

## [1.6.2.0]

### Changed
- **Lokalisierung**: Korrektur und vervollständigung der Lokalisierung.

## [1.6.0.0]

### Added
- **Sprachauswahl**: Das Dropdown-Menü für die Sprache zeigt jetzt den Eigennamen jeder Sprache (z. B. „Deutsch“, „English“, „Español“) an. Die Namen werden dynamisch aus den Sprachdateien (LANG_SELF) geladen.

### Changed
- **Sprachauswahl**: Die bisherige Anzeige von Sprachcode + Name wurde durch die Anzeige des Eigenbezeichnung ersetzt, um die Auswahl für Nutzer klarer und internationaler zu gestalten.

## [1.5.1.0]

### Added
- **Tools / Routen**: Neue Unterseite `Routen` mit Übersicht aktueller IPv4-Routen, direktem Löschen (abhängig von Klassifizierung) und Bereich zum Hinzufügen persistenter Routen.
- **Routen-Analyse**: Ziel-IP-Filter auf der Routen-Seite, der nur die für das Ziel relevanten Kandidaten gemäß Routing-Entscheidung anzeigt (Longest Prefix Match + Metrik).
- **Routen-Tabelle**: Sortierbare Spaltenköpfe (Zielnetz, Subnetzmaske, Gateway, Metrik) inkl. Sortierrichtungsanzeige.
- **IP-Profile / Routen**: IP-Profile um erweiterte Routenfunktionen ergänzt (Routen im Profil verwalten, Route-Modus für Anwenden/Hinzufügen, Abgleich mit bestehenden Systemrouten).

### Changed
- **Adapter-Status**: Statuskarten in der Adapter-Ansicht auf Dual-Stack erweitert (IPv4 links, IPv6 rechts) für NIC 1 und NIC 2.
- **Tools / Routen Layout**: Karten und Tabellenbreiten reagieren jetzt auf die Fensterbreite; Aktionsspalte stabilisiert mit Platzhaltertext `Systemroute` bei nicht löschbaren Einträgen.
- **Tools-Navigation**: Neue Tool-Unterseite `Routen` in die Navigation und Sichtbarkeitskonfiguration integriert.

### Fixed
- **Tray / Mausover**: Nach Klick auf das Systray-Icon (links oder rechts) wird das Infofenster für 10 Sekunden unterdrückt.
- **Routen-Einlesen**: Default-Route `0.0.0.0 / 0.0.0.0` wird korrekt in der Routenliste angezeigt.
- **Routen-Klassifizierung**: Löschbarkeit basiert auf Routing-Quelle (u. a. `Get-NetRoute` Protokoll und persistente Routen), nicht nur auf Erstellung über die Tool-Seite.

## [1.4.2.0]

### Added
- **Hover-Fenster**: Position am rechten Bildschirmrand ist jetzt in den Einstellungen konfigurierbar (`oben` oder `unten`, Abstand von rechts, Abstand von oben/unten).
- **Einstellungen**: Die Mausover-Konfiguration wird in `%APPDATA%\neTiPx\User_Settings.xml` über zusätzliche `hoverWindow`-Attribute gespeichert.

### Changed
- **Einstellungen**: Im Bereich `Maus Over Info Fenster` stehen `Anzeige` und `Verzögert` jetzt nebeneinander; darunter folgen Ausrichtung und Pixelabstände.
- **Hover-Fenster**: Die Position richtet sich nicht mehr nach der Maus, sondern nach einer festen Kante des aktuellen Bildschirms.

### Fixed
- **Hover-Fenster**: Die eingeblendete Schließen-Schaltfläche (`X`) beim Überfahren des Fensters wurde entfernt.

## [1.4.1.0]

### Added
- **Seiten-Sichtbarkeit**: Neue Konfiguration über `%APPDATA%\neTiPx\PagesVisibility.xml` für Hauptseiten und Tool-Unterseiten.
- **Einstellungen**: Versteckter Trigger auf der Settings-Seite (Wort `Wünschen`) öffnet einen Dialog zur Pflege der `PagesVisibility.xml`.
- **Einstellungen**: Dialog mit gruppierten Checkboxen (`Hauptseiten`, `Tools`) und Abhängigkeit zwischen `Tools (Hauptseite)` und Tool-Unterseiten.

### Changed
- **Navigation Hauptmenü**: Beim Start und nach Aktualisierung wird immer die erste sichtbare Seite fokussiert.
- **Navigation Tools**: Beim Öffnen von Tools wird immer die erste sichtbare Tool-Seite fokussiert.
- **Sichtbarkeitsregeln**: `Adapters`, `Info` und `Einstellungen` sind immer sichtbar und von der XML-Steuerung ausgeschlossen.
- **Einstellungen**: Bei ausgeblendeten Bereichen bleibt die Überschrift sichtbar; darunter wird `Keine Settings vorhanden` angezeigt.

### Fixed
- **Systray**: IP-Profil-Untermenü wird ausgeblendet, wenn `IP-Konfiguration` deaktiviert ist.
- **Netzwerkscanner**: Exception-Flut im Debug-Output reduziert (robuster Port-Check, detailbezogene Nachladung pro ausgewähltem Gerät).

## [1.4.0.3]

### Changed
- **Tools-Seite**: Der Bereich Netzwerkscanner wurde in eine eigene Unterseite (`NetworkScannerPage`) ausgelagert.
- **Tools-Seite**: Der Bereich WLAN wurde in eine eigene Unterseite (`WlanPage`) ausgelagert und zusammen mit dem Netzwerkscanner nach `Views/Tools` verschoben.
- **Tools-Seite**: Der Bereich Netzwerk-Rechner wurde in eine eigene Unterseite (`NetworkCalculatorPage`) ausgelagert und unter `Views/Tools` strukturiert.
- **Tools-Seite**: Der Bereich PING wurde in eine eigene Unterseite (`PingPage`) ausgelagert; die `ToolsPage` dient jetzt als Host mit Lazy-Loading der Tool-Unterseiten.

## [1.3.3.1]

### Fixed
- **Netzwerkscanner**: Geräte liste scrollbarverhalten verbessert

## [1.3.3.0]

### Added
- **Netzwerkscanner**: Neues Tool zum Scannen eine IP Netzwerk bereichs nach offenen Ports
- **Netzwerkscanner**: Konfiguration der zu Scannenen Ports
- **Netzwerkscanner**: Offene Ports über Doppelklick mit Default App öffnen

## [1.3.2.0]

### Added
- **Netzwerk-Rechner**: Neues Tool für IPv4- und IPv6-Subnetz-Berechnungen
- **Netzwerk-Rechner**: IPv4-Bereichserkennung (Privat, Public, Loopback, Zeroconf/Link-Local, Multicast, Broadcast, CGNAT, Dokumentation, Reserviert, Unspecified)

## [1.3.0.2]

### Added
- **WLAN Scanner**: Neue sortierbare "Band"-Spalte in der WLAN-Tabelle (zeigt 2.4G, 5G oder 6G)

## [1.3.0.1]

### Added
- **WLAN Scanner**: Neues Tool zum Scannen verfügbarer WLAN-Netzwerke

### Changed
- Tools-Seite um WLAN-Scanner erweitert (neben PING Tool)

### Fixed
- COM-Exceptions beim WLAN-Scan durch korrekte Dispatcher-Verwendung behoben

## [1.2.0.1]

### Added
- Aufgelöste IP-Adressen werden in der Ping-Statistik angezeigt (unterhalb jeder Zeile für Hostnamen)
- Aufgelöste IP-Adressen werden ins Ping-Log geschrieben

### Changed
- Ping-Ziele werden jetzt in XML-Datei (PingTargets.xml) statt config.ini gespeichert
- Ping-Log-Format optimiert: `Protokoll: Zeit;DN;IP;Antwortzeit`
- Nicht verfügbare Werte (DN/IP) werden als "nicht bekannt" ins Log geschrieben

## [1.2.0.0]

### Added
- Ping-Logging pro Ziel mit eigener Log-Datei
- Konfigurierbarer Ping-Log-Ordner in den Einstellungen (inkl. Ordnerauswahl und Reset auf Standard)
- Log-Aktionen pro Ping-Ziel: Log öffnen, beim Löschen mitlöschen oder vorher speichern
- Option "im Hintergrund weiter aktiv" auf der Ping-Seite

### Changed
- Ping-Konfiguration erweitert: Checkbox in der Kopfzeile für Hintergrundbetrieb
- Dynamische Pfadanzeige für den Ping-Log-Ordner auf eine Zeile optimiert (aggressiver Ausbau mit Fensterbreite)
- Anzeigeverhalten für nicht genutzte Protokolle vereinheitlicht: Antwortfeld zeigt "inaktiv" bei grauer Ampel

### Fixed
- Protokollspezifische Ping-Auswertung und Logging für IPv4/IPv6 (nur relevante Protokolle werden verarbeitet)
- Reaktivieren eines deaktivierten Ping-Ziels setzt den Protokollstatus korrekt zurück (z. B. "inaktiv" statt "Deaktiviert")

## [1.1.6.5]

### Changed
- Tools Page eingeblendet

## [1.1.6.4]

### Added
- IP-Profile können direkt aus dem Systray-Untermenü angewendet werden (ein Klick)
- Tooltips für alle Aktionen

### Changed
- Subnetz-Hinweis von Placeholder auf Tooltip umgestellt (Beispiel: 255.255.255.0, /24 oder 24)

## [1.1.6.3]

### Fixed
- NSIS Installer: Umlaute (ö, ä, ü) werden jetzt korrekt dargestellt (UTF-8 mit BOM)
- NSIS Installer: AppVersion werden jetzt korrekt dargestellt
- Tray-Kontextmenü: Umlaute werden korrekt angezeigt

## [1.1.6.2]

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
