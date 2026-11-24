# CLAUDE.md

Solo-developer repo, no collaborators: skip reviewer/CODEOWNERS/approval workflows; keep process lightweight.

Command Palette extension: lists Unity Hub projects (`%APPDATA%\UnityHub\hub.db`, Hub 3 fallback `projects-v1.json`) and opens them in the Unity Editor. Out-of-process COM server, MSIX-packaged, trimmed/AOT.

Priorities: runs reliably, then easy to develop and maintain. Package size and dependency count are not concerns: prefer a maintained library over hand-written interop.

Read before acting: `docs/PRODUCT.md` (scope, UX, roadmap), `docs/DEPENDENCIES.md` (updates), `docs/TROUBLESHOOTING.md` (errors, known warnings).
Read only when the topic comes up: `docs/DEVELOPMENT.md` (setup, dev loop, doc map), `docs/PUBLISHING.md` (packaging, signing, Store).

## Commands

Use the `justfile`; `just` lists recipes.

- Each change: `just build`, `just test`; after shell bulk edits `just check` (Claude hooks only cover Edit/Write).
- After a WindowsAppSDK/CsWinRT or packaging change: `just trim`, `just bundle`.
- New clone or new PC: `just doctor`.
- MSB3021/MSB3027 "locked by: Unity Extension for Command Palette" = extension is running: `just build <other dir>`; never kill it.
- Tests cover `ProjectParser` and `HubDatabase` only (the WAL test needs `pwsh`); UI and commands need a manual check in Command Palette (`just register`).
- `just register` resets release-install settings; dev settings survive.
- Push to `master` needs passing CI; land changes through a PR, direct pushes are rejected.

## Invariants

- CLSID: `[Guid]` in `UnityExtension.cs` == both `ClassId` entries in `Package.appxmanifest`.
- `just versions` passes (csproj and manifest versions agree); the tag build fails otherwise.
- Trim/AOT-safe code, no reflection unless an upstream API needs it; keep `sealed partial` where generators need it.
- Package versions only in `Directory.Packages.props`; `just pins` passes (shared packages <= PowerToys' pins).
- `ProjectParser` swallows exceptions and returns an empty list on failure.
- Settings and palette are separate windows: `CommandResult.Confirm` shows only in the palette, so settings actions confirm inline in their Adaptive Card.
- Hub data: the only writes delete `projects` rows in `hub.db` via `HubDatabase` (one or all); nothing else under `%APPDATA%\UnityHub`. Never read `accounts.db`, never log or show raw project-record JSON.
- Never run, require or bundle the Unity CLI (`unity`); `docs/reference/unity/cli.md` is reference only.
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
