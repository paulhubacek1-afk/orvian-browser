# Orvian Browser

A privacy-first, customizable browser for Windows with no account required.

## Goals

- Chromium-based browsing via Microsoft Edge WebView2 Runtime
- Local ad/tracker blocking with user-editable rules
- Phishing protection with a local blocklist and optional remote threat intelligence
- Password manager with encrypted local storage; passwords are never logged in plaintext
- Pwned Passwords checking using HIBP's k-anonymous range API
- Custom colors, themes, toolbar layout, keyboard shortcuts and per-site blocker rules
- Install websites as apps (PWA/windowed shortcuts)
- `Alt+C` screen/browser lock and `Alt+D` unlock
- Version checker and GitHub Release updater
- Open source, no account, no telemetry by default
- Installer and uninstaller
- Lightweight startup and lazy-loaded services

## Security model

Orvian must never implement a keystroke logger. The password feature is a password manager: credentials are captured only by explicit browser form-save actions and stored encrypted using Windows DPAPI tied to the current Windows user.

For password-leak checks, Orvian hashes the password locally and only sends the first five SHA-1 characters to the Pwned Passwords range API. The complete password and complete hash stay local.

## Important limitations

No browser can honestly guarantee that an ad blocker is invisible to every website or that YouTube will never detect blocking. Orvian therefore provides configurable blocking, per-site exceptions and a safe default mode rather than claiming impossible universal undetectability.

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
