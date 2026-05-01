# Changelog neTiPx

Alle wichtigen Änderungen dieses Projekts werden in dieser Datei dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.7.1.9]

### Fixed
- **IP-Konfiguration / UNC-Profil-Dropdown**: Breite auf 180 px reduziert; das Dropdown war zuvor zu breit und hat das Layout gestreckt.
- **Routen konfigurieren / Farbprofile Weiß & Prinzessin / Texte**: Sekundäre Beschriftungen und Spaltenköpfe im Routen-Dialog verwenden jetzt `AppTextSecondaryBrush` statt `TextFillColorSecondaryBrush`; Texte sind damit auf hellen Hintergründen wieder lesbar.
- **Routen konfigurieren / Farbprofile Weiß & Prinzessin / Buttons**: Accent-Buttons (Neu Einlesen, Route hinzufügen) erhalten jetzt explizit `AccentButtonForeground`; Text war zuvor schwarz statt weiß.
- **Routen konfigurieren / Separates Fenster / Theme-Sync**: Separates Routen-Fenster übernimmt jetzt das `RequestedTheme` des Hauptfensters, damit Theme-Ressourcen konsistent ausgewertet werden.
- **Routen konfigurieren / Separates Fenster / Übernehmen-Button**: Text des Übernehmen-Buttons ist bei hellen Farbprofilen (Weiß, Prinzessin) nun weiß; bei dunklen Profilen wird die Farbe automatisch über den `AccentButtonStyle` gesteuert.

## [1.7.1.8]

### Changed
- **Tools / UNC-Profile / Laufwerksauswahl**: Laufwerks-Dropdown auf feste Liste `A:` bis `Z:` vereinheitlicht;

### Added
- **Tools / UNC-Profile / Verbindungen**: Neuer Bereich rechts unten mit aktuell verbundenen UNC-Pfaden im System.
- **Tools / UNC-Profile / Trennen**: Pro verbundener Zeile ein Trennen-Button (Mülleimer-Symbol) hinzugefügt.
- **Tools / UNC-Profile / Auto-Refresh**: Liste der bestehenden UNC-Verbindungen wird nach Anwenden und nach Trennen automatisch neu eingelesen.
- **Tools / UNC-Profile / Tooltip**: Bei Mouseover über dem Verbindungsfeld wird der vollständige UNC-Pfad als Tooltip angezeigt.

### Fixed
- **Tools / UNC-Profile / Anzeige**: Persistierte Laufwerksbuchstaben werden in der vereinfachten Architektur wieder konsistent im Dropdown dargestellt.

## [1.7.1.7]

### Changed
- **Tools / UNC-Profile / Eingabe-UX**: Änderungen in Profilname, UNC-Pfad und Benutzername werden jetzt sofort erkannt (`UpdateSourceTrigger=PropertyChanged`) statt erst nach Fokusverlust.
- **Tools / UNC-Profile / Profilnamenliste**: Linke Profil-Liste aktualisiert den Namen beim Umbenennen live und zeigt den Dirty-Status konsistent über `DisplayName`.
- **Tools / UNC-Profile / Profilwechsel**: Wechsel auf ein anderes Profil folgt jetzt dem gleichen Ablauf wie bei IP-Profilen (Auswahlhandling im Code-Behind inkl. Save-/Discard-Entscheidung).

### Added
- **Tools / UNC-Profile / Unsaved-Dialog**: Beim Verlassen eines UNC-Profils mit ungespeicherten Änderungen erscheint ein Dialog zum Speichern oder Verwerfen.
- **Tools / UNC-Profile / Passwortfeld**: Passwortänderungen werden aus der `PasswordBox` ins Modell synchronisiert und lösen damit zuverlässig die Dirty-Logik aus.
- **Tools / UNC-Profile / Passwort-Reveal**: Standard-`PasswordBox` mit `PasswordRevealMode=Peek` aktiviert (Auge im Feld, Klartext nur solange gedrückt).

### Fixed
- **Tools / UNC-Profile / Dirty-Erkennung**: Feedback-Schleife bei `DisplayName`-PropertyChanged bereinigt; Sternanzeige und Änderungszustand bleiben stabil.
- **Tools / UNC-Profile / Passwort bei Profilwechsel**: Synchronisierung der `PasswordBox` beim DataContext-Wechsel verbessert, damit gespeicherte Werte nach Profilwechsel korrekt angezeigt werden.

## [1.7.1.6]

### Changed
- **Installer / BuildRelease**: Download der Windows App SDK Runtime ist jetzt versionsdynamisch und wird nicht mehr über eine fest eingetragene URL-Version gesteuert.
- **Installer / Versionsprüfung**: Ermittelt die aufgelöste `Microsoft.WindowsAppSDK`-Version direkt aus `neTiPx/obj/project.assets.json` nach dem Publish-Schritt.
- **Installer / Download-Logik**: Verwendet automatisch `https://aka.ms/windowsappsdk/<major.minor>/latest/windowsappruntimeinstall-x64.exe` passend zur aktuell verwendeten SDK-Haupt-/Nebenversion.
- **Installer / Cache**: Lokale Version wird in `Installation/WindowsAppRuntimeInstall.version` gespeichert; erneuter Download erfolgt nur bei Versionswechsel oder fehlender Installer-Datei.

### Added
- **Installer / Setup-Voraussetzung**: NSIS-Setup enthält `WindowsAppRuntimeInstall-x64.exe` als Prerequisite und startet die Runtime-Installation still (`--quiet`) vor der App-Installation.

## [1.7.1.5]

### Added
- **Adapter / Statuskarten / Reachability**: Live-Trendkurven (EKG-artig) für Gateway, DNS 1 und DNS 2 ergänzt, jeweils für IPv4 und IPv6 bei NIC 1 und NIC 2.
- **Tools / Ping / Verlauf**: Pro Ping-Ziel zusätzliche Trendkurven für IPv4 und IPv6 ergänzt.

### Changed
- **Adapter / Statuskarten / Layout**: Statusblöcke als 2-Spalten-Layout angepasst; links Status/IP/Ping, rechts die Trendkurve über die Gesamthöhe der drei Zeilen.
- **Adapter / Reachability / Monitoring**: Prüfintervall für Gateway/DNS von 5 Sekunden auf 1 Sekunde reduziert.
- **Adapter / Trendkurven / Darstellung**: Y-Skalierung auf dynamische Bereiche umgestellt, damit auch kleine Latenzänderungen sichtbar bleiben.
- **Tools / Ping / Layout**: Trendkurven unter die Statistikzeile (IP/Ø/Max/Timeout) verschoben und auf die jeweilige verfügbare Breite von IPv4/IPv6 ausgedehnt.
- **Tools / Ping / Trendkurven**: Kurvenhöhe auf ca. zwei Textzeilen eingestellt.
- **Systray / Hover-Fenster / Layout**: Fensterbreite angepasst, damit lange IPv6-Adressen seltener umbrechen.
- **Systray / Hover-Fenster / DPI**: Fensterbreite und Höhenanpassung werden jetzt mit der Windows-Bildschirm-Skalierung berechnet, damit das Fenster auf unterschiedlichen DPI-Stufen konsistent bleibt.

## [1.7.1.4]

### Changed
- **App / Fenstergröße / DPI**: Mindestbreite und Mindesthöhe werden jetzt DPI-sensitiv aus den Basiswerten berechnet, damit die Größe bei abweichender Windows-Skalierung konsistent bleibt.
- **App / Multi-Monitor**: Beim Verschieben des Fensters auf einen Monitor mit anderer Skalierung wird die Mindestgröße neu berechnet und angewendet.
- **App / Arbeitsfläche**: Die berechnete Mindestgröße wird auf die verfügbare Arbeitsfläche des aktuellen Displays begrenzt.
- **Navigation / Pane-Breite**: Umschalten zwischen offenem und geschlossenem Menü (`UpdateMinWidth`) berücksichtigt jetzt ebenfalls die aktuelle DPI-Skalierung.

## [1.7.1.2]

### Fixed
- **Tools / Log Viewer / Datei-Dialoge**: Öffnen, Import und Export von Dateien funktionieren wieder stabil in Entwicklungs- und Release-Umgebungen.
- **Tools / Log Viewer / Stabilität**: Dialogaufrufe von WinRT-Pickern auf native Windows-Dateidialoge umgestellt, um COM-Fehler (`0x80004005`) zu vermeiden.
- **Interop / Dateidialoge**: `OPENFILENAME`-Marshalling korrigiert, wodurch `ArgumentException` mit `0x80070057` beim Öffnen, Importieren und Exportieren behoben wurde.
- **IP-Profile / Manuelle IP-Eingabe**: Subnetzmasken- und CIDR-Eingaben werden nicht mehr automatisch überschrieben; Werte bleiben unverändert im Feld und werden nur validiert.
- **IP-Profile / Validierung**: Ungültige Subnetz-/CIDR-Werte blockieren Speichern und Anwenden weiterhin und markieren das betroffene Feld rot.
- **Tools / Routen / CIDR**: CIDR-Eingaben (z. B. `24` oder `/24`) werden vor `route ... mask ...`-Befehlen in Punkt-Subnetzmasken umgewandelt, damit Routen korrekt gesetzt/gelöscht werden.

## [1.7.1.0]
### Fixed

-Fehlende Keys in Sprachdateien
-überprüfung der Übersetzungen auf vollständigkeit ( wenn noch was fehlt bescheid sagen)

## [1.7.1.0]

### Changed
- **Log Viewer / Live-Update**: Nachladen neuer Logeinträge auf inkrementelles Anhängen umgestellt; Auto-Scroll lässt sich separat steuern und bleibt bei deaktivierter Option auf der aktuellen Position stehen.
- **Navigation / Hauptmenü**: Breite des erweiterten WinUI-`NavigationView`-Bereichs ist über `OpenPaneLength` gezielt festgelegt; komprimierte Breite bleibt über `CompactPaneLength` definiert.
- **Log Viewer / Highlight-Dialog**: Farbauswahl von Textliste auf visuelle Farbswatches (Rechteck) umgestellt.
- **Log Viewer / Highlights**: Farbpalette für Hervorhebungen auf 16 auswählbare Farben erweitert.
- **Log Viewer / Lokalisierung**: Alle sichtbaren Texte auf der Log-Viewer-Seite inkl. Highlight-Dialog vollständig auf Sprachschlüssel umgestellt.

]
## [1.7.0.0]

### Added
- **Tools / Log Viewer**: Neue Unterseite `Log Viewer` zum Öffnen, Anzeigen und Durchsuchen von Logdateien mit Dateiauswahl, Filter und Live-Anzeige.

### Changed
- **Log Viewer / Live-Update**: Nachladen neuer Logeinträge auf inkrementelles Anhängen umgestellt; Auto-Scroll lässt sich separat steuern und bleibt bei deaktivierter Option auf der aktuellen Position stehen.

### Fixed
- **Log Viewer / Filter**: Live-Updates mit aktivem Filter bewerten nur noch neu hinzugekommene bzw. geänderte Zeilen, statt die gesamte Ansicht sichtbar neu aufzubauen.

## [1.6.8.0]

### Added
- **Netzwerkscanner / Einstellungen**: Maximale Anzahl Hosts pro Scan ist jetzt in den Einstellungen konfigurierbar und wird in den Benutzereinstellungen gespeichert.

### Changed
- **Netzwerkscanner / Begrenzung**: Die bisher feste Host-Obergrenze wurde durch einen konfigurierbaren Grenzwert ersetzt; bei Überschreitung erscheint eine passende Fehlermeldung mit dem aktuell erlaubten Limit.
- **Routen / Einlesen**: Einlesen persistenter und statischer Routen intern optimiert und robuster zusammengeführt.
- **IP-Profile / Monitoring**: Verbindungsüberwachung für Gateway und DNS intern überarbeitet, um überlappende Statusprüfungen zu vermeiden und den Monitoring-Ablauf stabiler zu machen.

## [1.6.7.0]

### Changed
- **Installer**: NSIS-Installer auf minimale Installation vereinfacht

## [1.6.6.0]

### Changed
- **IP-Profile / Validierung**: IPv4-Syntaxprüfung für IP-, Gateway- und DNS-Felder auf striktes IPv4-Format vereinheitlicht (leer bleibt weiterhin erlaubt).
- **IP-Profile / UX**: Änderungen in Textfeldern werden sofort erkannt (Live-Update bei Eingabe, nicht erst beim Verlassen des Felds).
- **IP-Profile / Aktionen**: Statuslogik für `Speichern`/`Anwenden` auf Änderungszustand (`IsDirty`) umgestellt.

### Fixed
- **IP-Profile / Stabilität**: Absturz beim Löschen einer Manual-IP-Zeile behoben.
- **IP-Profile / Stabilität**: Absturz beim Hinzufügen weiterer IP-Zeilen nach vorherigem Speichern behoben (Rekursionsschutz in Validierungsfluss).
- **IP-Profile / Löschen**: Letzte IP-Zeile kann nicht mehr gelöscht werden; erste Zeile ist nicht löschbar.
- **IP-Profile / Profilname**: Umbenennen eines Profils erzeugt keinen zusätzlichen Profil-Eintrag mehr, sondern führt ein echtes Rename durch.
- **Lokalisierung**: Sprachschlüssel `IPCONFIG_SUBNET_MASK` in allen Sprachdateien ergänzt.

## [1.6.5.0]

### Changed
- **Adapter / IPv4**: IP-Adressen werden jetzt in CIDR-Notation angezeigt (z. B. `192.168.1.10/24`).
- **Adapter / IPv6**: IPv6-Adressen werden ebenfalls mit Präfixlänge angezeigt (z. B. `2001:db8::1/64`).
- **Systray / Hover-Fenster**: IP-Adressen werden auch im Mausover-Infofenster in CIDR-Notation angezeigt.

### Fixed
- **Systray / Hover-Fenster**: Wenn die Maus das Tray-Icon verlässt, wird ein versehentlich geöffnetes Mausover-Infofenster nach 5 Sekunden automatisch ausgeblendet.

## [1.6.4.0]

### Added
- **Anwendungsstart**: Die App läuft jetzt als Single-Instance-Anwendung. Ein zweiter Start aktiviert stattdessen das bereits laufende Hauptfenster.

### Changed
- **Fensteraktivierung**: Minimierte Hauptfenster werden beim Reaktivieren aus dem Tray oder durch einen erneuten App-Start korrekt wiederhergestellt.

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
