# Changelog

Alle nennenswerten Änderungen werden hier versioniert dokumentiert. Jeder veröffentlichte Build bekommt eine eigene Versionsnummer und einen eigenen GitHub-Release-Eintrag.

## [0.2.1] - 2026-09-07

### Installer-Bugfix
- Kritischen Startfehler des Installers behoben, der beim Öffnen wegen einer zu frühen `{app}`-Konstantenauflösung abstürzte.
- Den problematischen `UninstallDisplayIcon`-Eintrag entfernt, der den Fehler während des frühen Installer-Starts auslösen konnte.
- Reparatur-, Update- und Deinstallationslogik bleibt erhalten.
- 0.2.1 wird als neuer Release veröffentlicht, damit bestehende 0.2.0-Installationen das Update automatisch erkennen können.

## [0.2.0] - 2026-09-07

### Browser-Ausbau
- Eigene Orvian-New-Tab-Seite mit Suche und Schnellzugriffen hinzugefügt.
- Interne `orvian://`-Seiten für New Tab, Verlauf, Lesezeichen, Downloads, Datenschutz und Berechtigungen ergänzt.
- Command Palette mit `Ctrl+K` und Suchfilter eingebaut.
- `Ctrl+L`, `Ctrl+T`, `Ctrl+H`, `Ctrl+J`, `Ctrl+R` und `Ctrl+D` als Browser-Kurzbefehle ergänzt.
- Lokaler Verlauf mit bis zu 500 eindeutigen Einträgen hinzugefügt.
- Lokale Lesezeichenablage mit bis zu 500 Einträgen hinzugefügt.
- Download-Ziel standardmäßig auf den Windows-Downloads-Ordner gesetzt und Download-Ansicht ergänzt.
- Website-Berechtigungen für Kamera, Mikrofon, Standort und weitere WebView2-Berechtigungsarten werden abgefragt und lokal protokolliert.
- Datenschutz-Center mit sichtbarer Filterregelanzahl ergänzt.

### Panda-Maskottchen
- Eigenen animierten Panda-Controller integriert.
- Idle-Atmung, Blinzeln, Bounce, Squash-&-Stretch und Rotationsanimationen ergänzt.
- Kontextreaktionen für Start, Erfolg, Laden, Fehler, Updates, Datenschutz und Downloads eingebaut.
- Der Panda reagiert damit nicht mehr nur beim ersten Start, sondern auf wichtige Browser-Ereignisse.

### Kompatibilität
- IP-/LAN-Navigation aus 0.1.2 beibehalten und in die neue Navigationslogik integriert.
- uBlock-Origin-kompatible Netzwerkfilter aus 0.1.2 beibehalten.
- Self-contained Windows-x64-Build und eingebettetes Orvian-Icon beibehalten.

## [0.1.2] - 2026-09-07

### Große Bugfix-Version
- Erste-Start-Willkommensanimation mit dem Orvian-Panda-Maskottchen hinzugefügt.
- IP-Adressen funktionieren jetzt direkt, einschließlich lokaler IPv4-/IPv6-Adressen und typischer HTTP-Geräteadressen.
- Oberfläche überarbeitet: moderneres Chrome-ähnliches Toolbar-Layout, weichere Karten und animierte Startansicht.
- Orvian-Icon wird nun während des CI-Builds vor dem Publish als `ApplicationIcon` eingebettet und ist damit Teil der EXE.
- Werbe-/Tracker-Schutz über einen uBlock-Origin-kompatiblen Netzwerkfilter mit gecachten EasyList-/EasyPrivacy-Regeln verbessert.
- Filter werden beim Start aus dem lokalen Cache geladen und anschließend regelmäßig aktualisiert.
- Update-Prüfung verwendet jetzt die tatsächliche Assembly-Version statt einer fest verdrahteten Versionsnummer.
- Orvian prüft beim Start und anschließend regelmäßig auf neue GitHub-Releases und kann den neuen Installer herunterladen und starten.
- Installer erkennt bestehende Installationen und bietet `Reparieren`, `Updaten` und `Deinstallieren` an.
- Release-/Installer-Versionen bleiben über `VERSION.txt` und die GitHub Actions synchron.

### Hinweise zu externen Filtern
- uBlock Origin selbst wird nicht als Browser-Code eingebettet. Orvian nutzt stattdessen kompatible Netzwerkfilter und lädt uBlock-Origin-verwaltete Filterquellen zur Laufzeit.

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
