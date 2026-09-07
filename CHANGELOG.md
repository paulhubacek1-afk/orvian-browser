# Changelog

## [1.0.1] - Big bugfix

### Stability
- Removed the 250 ms tab synchronization polling loop; tab UI synchronization is now event-driven where possible with a low-frequency safety reconciliation.
- Added cleanup for UI timers when the main window closes.
- Chromium-style tab keyboard navigation remains available without continuously rebuilding the tab strip.
- New tabs are reconciled after tab creation actions so their headers cannot silently disappear.

### Updates
- Startup update checking remains immediate and is backed by the stable-release scan plus VERSION.txt fallback.
- Release builds now have mandatory version/changelog validation and a real Release-mode compile before packaging.
- GitHub Actions now separates PR/build validation from actual release publishing.
- Installer SHA-256 checksums are generated for every verified release build.

### Mascot
- Refreshed the Panda mascot with a fuller body, paws, ears, facial highlights and smoother idle/activity animation.
- Panda animation timers are stopped when the browser closes.

## [1.0.0] - Stable

### Release audit
- Browser chrome reduced to two compact rows; the old extra status/action row is gone.
- The native Windows window controls are used instead of a second custom set of close/minimize/maximize controls.
- The animated Panda is isolated in its own browser-content layer and no longer sits in the tab/title area or inside the new-tab HTML.
- Internal `orvian://` pages no longer expose WebView2's implementation-level `data:` URL in the address bar.

### Web and navigation
- Normal HTTP/HTTPS documents are never rejected merely because a hostname appears in the ad/tracker filter list.
- Top-level document requests are left untouched; only subresource requests are eligible for network filtering.
- Local `file://` HTML, HTM files, `data:text/html` documents and raw HTML input are supported.
- `target="_blank"` and WebView2 new-window requests now create a real new Orvian tab.
- `Ctrl+T` creates a new tab without replacing the current one.
- `Ctrl+W` closes only the active tab and automatically keeps one usable tab open.

### Privacy and performance
- Filter-list loading is moved off the synchronous startup path.
- Cached EasyList/EasyPrivacy data is reused while stale lists refresh in the background.
- The network filter keeps constant-time hostname lookups instead of a full linear rule scan.
- Filter exceptions are respected for supported host-based rules.
- Browser history, bookmarks and permissions use serialized asynchronous file access to avoid multi-tab write races.

### Passwords and security
- Password vault remains encrypted with Windows DPAPI for the current Windows user.
- Pwned Password checks use the k-anonymous range API.
- Supported WebView2 permissions are explicitly approved or denied and recorded locally.

### Updates and release
- Update detection checks a cache-busted remote `VERSION.txt` and GitHub Releases in parallel.
- A newer remote version can be detected even while its installer asset is still being published.
- The release workflow validates that `VERSION.txt`, `Version`, `AssemblyVersion` and `FileVersion` match.
- Release publishing is idempotent: an existing release is updated with the verified installer instead of causing the build to fail.
- Installer defaults and architecture settings were cleaned up for the final Windows x64 release.

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
