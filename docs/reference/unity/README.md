# Unity Hub and Unity CLI reference (Windows)

How Unity Hub 4 and the Unity CLI store and show project and editor data. Background for extension
features. Paths use `<user>`, `<project>` and `<version>` placeholders.

| File | Content |
|---|---|
| **What Unity does** | |
| [hub-data.md](hub-data.md) | Hub 4 data folder: databases, JSON files, project record schema |
| [hub-ui.md](hub-ui.md) | Hub 4 Projects page: columns, row states, details panel, tags |
| [cli.md](cli.md) | Unity CLI: commands, output formats, which data it reads |
| **What we do about it** | |
| [community.md](community.md) | Third-party launchers: launch args, extra data sources, pitfalls |
| [rea.md](rea.md) | rea toolkit: which tools help audit Hub, and how to run them safely |

Re-audit after a Hub or CLI upgrade with the `unity-hub-audit` skill.

## Versions covered

| Product | Version | Notes |
|---|---|---|
| Unity Hub | 4.0.x (4.0.0 GA 2026-11-13, 4.0.1 2026-11-16) | Native rewrite of the Electron Hub 3. MSIX on Windows |
| Unity CLI (`unity`) | 1.0.x (1.0.1 2026-11-16) | Separate MSIX package (winget `Unity.CLI`). Replaces Hub 3 `--headless` |
| Hub 3 | 3.22.6 is the last | Wrote `projects-v1.json` |

Hub 4 release post: https://discussions.unity.com/t/now-available-unity-hub-4-0-0/1741866
CLI manual: https://docs.unity.com/unity-cli

## Tech stack and packaging (from the Windows binaries and manifests)

| | Hub 4.0 | CLI 1.0 |
|---|---|---|
| Language | Rust (same `rustc` commit as the CLI) | Rust |
| UI | GPUI, Zed's GPU UI framework (git dep `gpui-ce`), DirectX rendering; `taffy` layout, `parley`/`swash`/`fontique` text, `resvg` SVG, `accesskit` accessibility, `tray-icon` + `muda` tray/menus, `wry` + WebView2 for embedded web pages (sign-in, feeds) | none |
| Shared crates | `tokio`, `reqwest`/`hyper` + `rustls`, `rusqlite` (bundled SQLite), `serde_json`, `sentry`, `tar`/`flate2` | same |
| Package | MSIX `UnityTechnologies.UnityHub`, alias `unityhub.exe`, protocol `unityhub://`, `runFullTrust`, `allowElevation`, Windows 10 21H1+ | MSIX `UnityTechnologies.UnityCLI`, alias `unity.exe`, `runFullTrust` |
| Contents | `Unity Hub.exe` (~76 MB), a copy of `unity.exe` (byte-identical to the CLI package), `UnityLicensingClient_V1\Unity.Licensing.Client.exe`, `assets\i18n` (10 locales), images | `unity.exe` (~45 MB), icons, `Update.appinstaller` |

Hub ships its own copy of the CLI. The Hub 3 Electron/Chromium folders in `%APPDATA%\UnityHub` are
leftovers.

## Key findings

1. **Hub 4 keeps projects in SQLite**, `%APPDATA%\UnityHub\hub.db`, table `projects`. On first launch
   it imports `projects-v1.json`, sets `meta.projects_json_migrated = 1`, and never writes the JSON again.
2. **The JSON goes stale.** Projects added, removed, starred or retagged in Hub 4 do not show up in it.
3. **Tags are Hub 4 only.** They live in the `hub.db` row (`tags` array), nowhere else.
4. **CLI 1.0.0 still reads `projects-v1.json`** (filtered by `excludedProjectFolders.json`), not
   `hub.db`. `unity projects` shows the stale list without tags. The 1.0.1 changelog does not mention
   a change. Hub and CLI share `%APPDATA%\UnityHub` but can disagree.
5. Branch, size, Git account and "missing editor" state are not stored. Hub computes them live.
