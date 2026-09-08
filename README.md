# Orvian Browser

A privacy-first, customizable Chromium-based desktop browser for Windows with no account required.
Wichtige info:
## Orvian Browser Team!
Nach reichlicher überlegung setze ich den Browser neu auf Mehr im Chromium Style das update kann zwischen 1 und 3 Wochen dauern! Bis dort hin könnt ihr Orvian nicht Downloaden! Tut uns leid!
Solltest du es besser hinbekommen überzeuge uns und Programmiere mit dem Original Source code deine eigenen Extensions oder den Browser komplett neu!
## Included

- Chromium-based browsing through Microsoft Edge WebView2
- Real multi-tab browsing with `Ctrl+T` / `Ctrl+W`
- New-tab page with fast search and browser shortcuts
- Local HTML and `data:` document support
- Normal `target="_blank"` / popup links open in a new Orvian tab
- Local history, bookmarks and download overview
- Password manager with encrypted Windows DPAPI storage
- Pwned Passwords checking using the HIBP k-anonymous range API
- Local permission prompts for supported WebView2 permissions
- Lightweight network filtering for recognized advertising and tracker resources
- Filter-list caching and background refresh
- Automatic update detection using both a cache-busted version endpoint and GitHub Releases
- Animated Orvian Panda as a separate browser companion
- Browser lock with `Alt+C` and unlock with `Alt+D`
- Command Palette with `Ctrl+K`
- Open source, no account, no telemetry by default
- Windows x64 installer with update, repair and uninstall handling

## Web and HTML behavior

Orvian does **not** block an entire website because a hostname appears in the filter list. The top-level document request is always allowed; the blocker only evaluates subresources requested by the page. This keeps ordinary sites such as Google and YouTube usable while still filtering recognized advertising and tracking resources.

To open a local HTML document, enter its full Windows path or a `file:///...` URL in the address bar. Raw HTML can also be entered directly when it starts with a normal HTML document tag; Orvian opens it as a `data:text/html` document.

## Security model

Orvian must never implement a keystroke logger. The password feature is a password manager. Stored credentials are encrypted with Windows DPAPI for the current Windows user.

For password-leak checks, Orvian hashes the password locally and only sends the first five SHA-1 characters to the Pwned Passwords range API. The complete password and complete hash stay local.

## Filter lists

Orvian uses a lightweight host/path filtering engine for common uBlock Origin / EasyList-style network rules. It downloads EasyList and EasyPrivacy data, caches the lists locally and parses network-oriented host rules. Cosmetic DOM filters are intentionally not implemented.

The upstream filter lists remain subject to their respective licenses.

## Limitations

No browser can honestly guarantee that every advertisement, tracker or anti-adblock mechanism will be handled perfectly. Orvian therefore uses conservative network filtering rather than claiming universal blocking or undetectability.

## Build

Requirements:

- Windows 10/11 x64
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime
- Inno Setup 6 (installer only)

```powershell
dotnet restore
dotnet build -c Release
```

The installer script is under `installer/Orvian.iss`.
