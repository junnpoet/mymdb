# MyMDB

A fast, lightweight **Android** and **Linux Desktop** viewer for **EMDB (Eric's Movie Database)** collections synced through Google Drive, built with **.NET 10**, **C#**, and **Avalonia UI 12**.

## Quick Start

```bash
# 1. Run unit and real-fixture tests
dotnet test

# 2. Run on Linux Desktop
dotnet run --project src/MyMdb.app.Desktop

# 3. Build and deploy to a connected Android device via adb
dotnet build src/MyMdb.app.Android/MyMdb.app.Android.csproj -t:Run
```

> **Prerequisites (Linux / Arch):** `.NET 10 SDK`, `jdk21-openjdk`, `.NET android` workload, and Android SDK in `~/Android/Sdk`.

## Current Progress

### Completed (MVP)

- [x] **EMDB v80 Parser (`MyMdb.Core`)**: Reads UTF-16 LE `emdb.dat`, sanitizes `0x1E` record separators and `<DQ>` quotes, decodes hex IMDb ratings, Metascore, cast/crew indices, flags, and TV series detection (`#` genre marker).
- [x] **Automated Tests (`MyMdb.Core.Tests`)**: Unit tests, 356-entry fixture integration test, and mocked `HttpMessageHandler` tests for Google Drive caching and path-traversal protection.
- [x] **Google Drive Sync & On-Demand Disk Cache**: Lightweight REST API v3 client over `HttpClient` with OAuth 2.0 + PKCE (`drive.readonly`), `modifiedTime` sync check for `emdb.dat`, and lazy local caching of posters, backdrops, and actor photos.
- [x] **Cross-Platform UI (`MyMdb.app`, Spanish)**:
  - Catalog list with button-triggered search (+ `Enter`), quick filters (*Todas / Películas / Series / Vistas / Sin ver*), dynamic genre dropdown, and 20-item pagination (*Cargar más*).
  - Movie and TV series detail view with backdrop/poster blurred loading overlay, synopsis, metadata, horizontal cast strip, comments, and links.
  - Explicit **Conectar Google Drive** welcome screen and icon-only sign-out with confirmation modal (preserves local cache).
- [x] **Android Build & Branding (`MyMdb.app.Android`)**: `com.mymdb.app`, embedded **Android Custom Tabs** OAuth flow (`Xamarin.AndroidX.Browser`), custom rounded **MyMDB** app icon, and safe-zone Android 12+ splash screen.

### Next Steps (Phase 2)

- [ ] Parse `TVSeries/<id>/<id>.dat` on demand to display seasons and episodes inside TV series details.
- [ ] Make external links (YouTube trailer, streaming URLs) tappable from the detail view.
- [ ] Configure signed `Release` APK builds for standalone installation.

## Architecture

| Project | Role | Dependencies |
| :--- | :--- | :--- |
| `src/MyMdb.Core` | Domain `record` models, EMDB parser, `ICatalogDataSource`, Google Drive REST v3 + OAuth PKCE client, disk cache | .NET 10 BCL only (no UI) |
| `src/MyMdb.app` | Shared Avalonia 12 UI (`NavigationPage`, MVVM with `CommunityToolkit.Mvvm`, Spanish labels, image decoding cache) | `MyMdb.Core` |
| `src/MyMdb.app.Desktop` | Linux Desktop entry point + loopback OAuth broker (`127.0.0.1`) | `MyMdb.app` |
| `src/MyMdb.app.Android` | Android entry point (`net10.0-android`) + Custom Tabs OAuth broker + adaptive splash icon | `MyMdb.app` |
| `tests/MyMdb.Core.Tests` | xUnit test suite with self-contained fixtures | `MyMdb.Core` |

## Credentials & Local Paths

OAuth credentials and tokens are never committed to Git.

| Purpose | Local Path | Notes |
| :--- | :--- | :--- |
| Desktop OAuth client | `~/.config/MyMDB/client.json` | Google Cloud **Desktop app** JSON (`chmod 600`) |
| Android OAuth client | `~/.config/MyMDB/android-client-id.json` | Google Cloud **Android** JSON (`com.mymdb.app`, Custom URI scheme enabled) |
| Saved OAuth token | `~/.local/share/MyMDB/gdrive-token.json` | App-private `FilesDir` on Android |
| Local disk cache | `~/.cache/MyMDB/` | App-private `CacheDir` on Android |
