# Changelog

## [0.3.0] - Stable

### Fixes
- Echter Multi-Tab-Modus: `Ctrl+T` öffnet einen neuen Tab, ohne den aktiven Tab zu ersetzen.
- `Ctrl+W` und die Tab-Schließen-Schaltflächen schließen nur den ausgewählten Tab.
- Update-Prüfung läuft unabhängig vom WebView2-Start und besitzt einen Fallback über `VERSION.txt`.
- Update-Erkennung funktioniert auch dann, wenn ein GitHub-Release vorübergehend keine EXE als Asset liefert.
- Passwort-Tresor speichert mehrere Logins dauerhaft und verschlüsselt sie pro Windows-Benutzer via DPAPI.
- Der Passwort-Tresor ist jetzt tatsächlich über die Einstellungen erreichbar.
- Werbeblocker wurde auf schnelle Hostname-Lookups umgestellt; die teure lineare Prüfung tausender Regeln pro Request entfällt.
- Download-Dateien bekommen bei Namenskonflikten automatisch einen freien Dateinamen.

### UI / UX
- Einstellungen vollständig überarbeitet: klarere Navigation, Kartenlayout und weniger visuelles Durcheinander.
- Neue Startseite mit stärkerem Orvian-Branding und modernerem Hero-Bereich.
- Panda reagiert jetzt dauerhaft: Blinzeln, Schweben und zufällige kleine Reaktionen statt einer einmaligen Begrüßungsanimation.
- Tab-Leiste zeigt mehrere echte Tabs mit aktivem Zustand und eigenen Schließen-Buttons.

## [0.2.1]

Vorherige Orvian-Version.
