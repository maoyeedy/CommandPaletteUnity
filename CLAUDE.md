# CLAUDE.md

PowerToys Command Palette extension: lists Unity Hub projects (`%APPDATA%\UnityHub\projects-v1.json`) and opens them in the Unity Editor. Out-of-process COM server, MSIX-packaged, trimmed/AOT.

Read before acting: `docs/DEVELOPMENT.md` (build, tooling), `docs/TROUBLESHOOTING.md` (errors), `docs/PUBLISHING.md` (packaging, signing, Store).

## Commands

Use the `justfile`; `just` lists recipes.

- Each change: `just build`, `just test`; after shell bulk edits `just check` (Claude hooks only cover Edit/Write).
- After a WindowsAppSDK/CsWinRT or packaging change: `just trim`, `just bundle`.
- New clone or new PC: `just doctor`.
- MSB3021/MSB3027 "locked by: Unity Extension for Command Palette" = extension is running: `just build <other dir>`; never kill it.
- Tests cover `ProjectParser` only; UI and commands need a manual check in PowerToys (`just register`).

## Invariants

- CLSID: `[Guid]` in `UnityExtension.cs` == both `ClassId` entries in `Package.appxmanifest`.
- `just versions` passes (csproj and manifest versions agree); the tag build fails otherwise.
- Trim/AOT-safe code, no reflection unless an upstream API needs it; keep `sealed partial` where generators need it.
- Package versions only in `Directory.Packages.props`; `just pins` passes (shared packages <= PowerToys' pins).
- `ProjectParser` swallows exceptions and returns an empty list on failure.
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
| Shmuelie.WinRTServer (2.2.1) | none; read its GitHub README |
| xUnit (2.9.3) | `/websites/xunit_net`; not `/xunit/xunit/v3_3_0_1` (v3) |
| CSharpier (1.3.0) | `/belav/csharpier` |
| MSIX / AppxManifest | `/microsoftdocs/msix-docs` |
| .NET 10 / dotnet CLI | `/dotnet/docs` |
