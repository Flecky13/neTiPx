# GitHub Actions Release Process

neTiPx verwendet einen automatisierten Release-Workflow ueber GitHub Actions.

Workflow-Datei:

- `.github/workflows/release.yml`

## Trigger

- Nur manueller Start ueber `workflow_dispatch` mit Parameter `release_tag`

## Was gebaut wird

- Windows (`windows-latest`)
  - .NET Publish (win-x64)
  - bestehender NSIS-Prozess wird genutzt
  - Ergebnis: `Setup.exe`
- Linux (`ubuntu-latest`)
  - .NET Publish (linux-x64)
  - bestehender `.deb`-Prozess wird genutzt
  - AppImage wird erzeugt
  - Ergebnis: `.deb` und `.AppImage`
- macOS (`macos-latest`)
  - .NET Publish
  - bestehender `.dmg`-Prozess wird genutzt
  - Ergebnis: `.dmg`

Alle Plattform-Builds laufen parallel als Matrix-Job.

## Wiederverwendung der bestehenden Build-Logik

Die Pipeline uebernimmt die vorhandenen lokalen Build-Schritte (Publish, NSIS, .deb, AppImage, .dmg) in repo-versionierte CI-Skripte.

CI-Helfer (Repo-tracked):

- `.github/scripts/build-windows-release.ps1`
- `.github/scripts/build-linux-release.sh`
- `.github/scripts/build-macos-release.sh`

## Release erstellen auf Wunsch

1. Version in `src/Directory.Build.props` aktualisieren.
2. Aenderungen committen und nach `master` pushen.
3. In GitHub Actions den Workflow `Release` manuell starten.
4. Als `release_tag` den gewuenschten Tag angeben (z. B. `v2.0.6`).
5. GitHub Release wird erstellt oder aktualisiert und Assets werden hochgeladen.

Optional (wenn Tag noch nicht existiert):

```bash
git tag v2.0.6
git push origin v2.0.6
```

## Manuell starten

In GitHub unter Actions den Workflow `Release` ausfuehren und `release_tag` setzen (z. B. `v2.0.6`).