# Changelog

Alle nennenswerten Änderungen werden hier versioniert dokumentiert. Jeder veröffentlichte Build bekommt eine eigene Versionsnummer und einen eigenen GitHub-Release-Eintrag.

## [0.1.1] - 2026-09-07

### Release-System
- Versionsverwaltung über `VERSION.txt` eingeführt.
- Jeder neue veröffentlichte Stand bekommt eine neue Versionsnummer.
- GitHub Actions verwendet die Versionsnummer für Installer, Release-Tag und Release-Titel.
- Ein Release wird nur erstellt, wenn der komplette Windows-x64-Build erfolgreich war.
- Bereits verwendete Versionsnummern werden nicht still überschrieben.
- Änderungsprotokoll (`CHANGELOG.md`) als feste Projektdatei eingeführt.

### Browser
- Aktueller Stand von Orvian Browser 0.1.1 veröffentlicht.
- Das bestehende Design-, Animations-, Einstellungs- und Schutzsystem bleibt Bestandteil des Releases.

## [0.1.0] - 2026-09-07

### Erstes öffentliches Release
- Erster öffentlicher Windows-x64-Build von Orvian Browser.
- Self-contained Veröffentlichung ohne separate .NET-Installation auf dem Ziel-PC.
- Offline-WebView2-Runtime im Installer enthalten.
- Windows-Installer und portable Build werden über GitHub Actions erstellt.
