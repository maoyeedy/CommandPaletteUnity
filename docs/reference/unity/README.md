# Unity Hub and Unity CLI reference (Windows)

How Unity Hub 4 and the Unity CLI store and show project and editor data. Background for extension
features. Paths use `<user>`, `<project>`, `<version>` and `<install dir>` placeholders.

| File | Content |
|---|---|
| **Unity Hub** | |
| [unityhub/protocol.md](unityhub/protocol.md) | `unityhub://` install and project links; Hub starts on demand |
| [unityhub/registry.md](unityhub/registry.md) | `hub.db` schema, project record fields, other databases |
| [unityhub/config-and-logs.md](unityhub/config-and-logs.md) | `%APPDATA%\UnityHub` JSON files, logs, folders, running indicators |
| [unityhub/install-locations.md](unityhub/install-locations.md) | Hub and editor install locations, discovery, installer registry keys |
| [hub-ui.md](hub-ui.md) | Hub 4 Projects page: columns, row states, details panel, tags |
| **Unity CLI** | |
| [cli.md](cli.md) | commands, output formats, which data it reads |
| **What we do about it** | |
| [community.md](community.md) | Third-party launchers: launch args, extra data sources, pitfalls |
| [rea.md](rea.md) | rea toolkit: which tools help audit Hub, and how to run them safely |

Re-audit after a Hub or CLI upgrade with the `unity-hub-audit` skill. For quick local fact gathering,
`just editors`, `just hubfacts` and `just docs-check` are Windows dev-only helpers (see `scripts/`).

## Versions covered

| Product | Version | Notes |
|---|---|---|
| Unity Hub | 4.0.x (4.0.0 GA 2026-11-13, 4.0.1 2026-11-16) | Native rewrite of the Electron Hub 3. MSIX on Windows |
| Unity CLI (`unity`) | 1.0.x (1.0.1 2026-11-16) | Separate MSIX package (winget `Unity.CLI`). Replaces Hub 3 `--headless`. CLI facts below were observed on 1.0.0; 1.0.1 notes come from its changelog |
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

Hub ships its own copy of the CLI at `<Hub package>\app\unity.exe`; it runs when started directly.
The Hub 3 Electron/Chromium folders in `%APPDATA%\UnityHub` are leftovers.

## Key findings

1. **Hub 4 keeps projects in SQLite**, `%APPDATA%\UnityHub\hub.db`, table `projects`. On first launch
   it imports `projects-v1.json`, sets `meta.projects_json_migrated = 1`, and never writes the JSON
   again (see [unityhub/registry.md](unityhub/registry.md)).
2. **The JSON goes stale.** Projects added, removed, starred or retagged in Hub 4 do not show up in it.
3. **Tags are Hub 4 only.** They live in the `hub.db` row (`tags` array), nowhere else; the UI and
   tag dialog are in [hub-ui.md](hub-ui.md), storage in
   [registry.md](unityhub/registry.md#project-record-fields).
4. **CLI 1.0.x reads and writes `hub.db`** once Hub 4 has migrated (observed on 1.0.0).
   `unity projects` returns the `hub.db` path set, including projects added after migration that the
   JSON lacks, still without tags. `projects add` / `remove` change only the `projects` row, not the
   JSON or `excludedProjectFolders.json` (see [cli.md](cli.md)).
5. Branch, size, Git account and "missing editor" state are not stored. Hub computes them live.
6. A `unityhub://` link starts Hub through the OS protocol handler; Hub does not need to be open
   first (see [unityhub/protocol.md](unityhub/protocol.md)).
