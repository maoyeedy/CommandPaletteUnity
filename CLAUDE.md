# CLAUDE.md

Solo-developer repo, no collaborators: skip reviewer/CODEOWNERS/approval workflows; keep process lightweight.

Command Palette extension: lists Unity Hub projects (`%APPDATA%\UnityHub\hub.db`, Hub 3 fallback `projects-v1.json`) and opens them in the Unity Editor. Out-of-process COM server, MSIX-packaged, trimmed/AOT.

Priorities: runs reliably, then easy to develop and maintain. Package size and dependency count are not concerns: prefer a maintained library over hand-written interop.

Read before acting: `docs/PRODUCT.md` (scope, UX), `docs/ROADMAP.md` (roadmap), `docs/DEPENDENCIES.md` (updates), `docs/TROUBLESHOOTING.md` (errors, known warnings).
Read only when the topic comes up: `docs/DEVELOPMENT.md` (setup, dev loop, doc map), `docs/PUBLISHING.md` (packaging, signing, Store).

## Commands

Use the `justfile`; `just` lists recipes.

- Each change: `just build`, `just test`; after shell bulk edits `just check` (Claude hooks only cover Edit/Write).
- After extension code edits: run `just register` automatically (do not ask first) so the live Command Palette session gets the change, and report whether it succeeded, failed, or Command Palette was not running. (`just register` already builds Debug x64 and reloads CmdPal, which immediately re-locks `bin/`, so do not run `just build` after it; it resets release-install settings while dev settings survive.)
- After a WindowsAppSDK/CsWinRT or packaging change: `just trim`, `just bundle`.
- New clone or new PC: `just doctor`.
- MSB3021/MSB3027 "locked by: Unity Extension for Command Palette" = extension is running: `just register` (replaces `just build`) or compile-check with `just build <other dir>`; never kill it.
- Tests cover `ProjectParser`, `HubDatabase`, `HubDataWatcher`, path shortening, and Hub install URI validation (the WAL test needs `pwsh`); UI and commands need a manual check in Command Palette after `just register`.
- Commit on a branch, not `master` (even for docs-only changes). Stop and ask before opening or merging a PR: the user may have follow-up changes.
- Push to `master` needs passing CI; land changes through a PR, direct pushes are rejected.

## Invariants

- CLSID: `[Guid]` in `UnityExtension.cs` == both `ClassId` entries in `Package.appxmanifest`.
- `just versions` passes (csproj and manifest versions agree); the tag build fails otherwise.
- Trim/AOT-safe code, no reflection unless an upstream API needs it; keep `sealed partial` where generators need it (including `IDisposable` -> `CsWinRT1028`).
- Package versions only in `Directory.Packages.props`; `just pins` passes (shared packages <= PowerToys' pins).
- `ProjectParser` swallows exceptions and returns an empty list on failure.
- Settings and palette are separate windows: `CommandResult.Confirm` shows only in the palette, so settings actions confirm inline in their Adaptive Card.
- Hub data: the only writes are `hub.db` `projects` row inserts (`INSERT OR IGNORE` new projects) and deletes (one or all) via `HubDatabase`; nothing else under `%APPDATA%\UnityHub`. Never read `accounts.db`, never log or show raw project-record JSON.
- Never run, require or bundle the Unity CLI (`unity`); `docs/reference/unity/cli.md` is reference only.
- Details pane metadata: use `DetailsTags` (`[new Tag(value)]`) for plain-text rows and `DetailsLink` only with a valid absolute `Uri` (avoids null-`Uri` WinRT marshaling issues).
- No Markdown under `UnityExtension/`.

## Context7 IDs

Unpinned (track default branch); confirm API details against the installed package.

| Library (repo pin) | ID |
|---|---|
| Microsoft.CommandPalette.Extensions (0.9.260303001) | `/websites/learn_microsoft_en-us_windows_powertoys`, source `/microsoft/powertoys` |
| Microsoft.WindowsAppSDK (2.2.0) | `/microsoft/windowsappsdk` |
| WinUI 3 | `/microsoft/microsoft-ui-xaml` |
| Microsoft.Windows.CsWinRT (2.2.0) | `/microsoft/cswinrt` |
| Microsoft.Windows.CsWin32 (0.3.275) | `/microsoft/cswin32` |
| Microsoft.Data.Sqlite (11.0.0) | `/dotnet/docs` (docs/standard/data/sqlite) |
| Shmuelie.WinRTServer (2.2.1) | none; read its GitHub README |
| xUnit (2.9.3) | `/websites/xunit_net`; not `/xunit/xunit/v3_3_0_1` (v3) |
| CSharpier (1.4.0) | `/belav/csharpier` |
| MSIX / AppxManifest | `/microsoftdocs/msix-docs` |
| .NET 10 / dotnet CLI | `/dotnet/docs` |

## Upstream source

`just vendor` creates a sparse, gitignored `vendor/PowerToys/` checkout; `just vendor-update` refreshes it. Read the Toolkit, extension template and sample pages there instead of `gh api repos/.../contents/...`. The repo URL and sparse paths live in `scripts/Init-Vendor.ps1`.
