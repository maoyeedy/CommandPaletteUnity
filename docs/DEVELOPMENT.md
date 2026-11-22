# Development Guide

The dev loop: setup, build, test, debug. Each other topic has one home:

| Topic | File |
|---|---|
| Scope, UX, roadmap | [PRODUCT.md](PRODUCT.md) |
| Unity Hub / CLI data formats | [reference/unity/](reference/unity/README.md) |
| NuGet, tool, hook and Windows SDK updates | [DEPENDENCIES.md](DEPENDENCIES.md) |
| Packaging, signing, releases, Store | [PUBLISHING.md](PUBLISHING.md) |
| Errors and known warnings | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |
| Upstream extension template | [PowerToys `ExtensionTemplate`](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension) |

The project started from that template. `Program.cs`, `UnityExtension.cs`, the publish profiles
and the packaging parts of the csproj should stay close to it; diff against it when bumping
`Microsoft.CommandPalette.Extensions` and when the extension misbehaves on start or exit.

## Machine setup

Install with winget, then run `just doctor`. Windows App Runtime, VCLibs and UI.Xaml arrive as MSIX
dependencies; do not install them by hand.

```powershell
# Windows SDK: version and pin rules in DEPENDENCIES.md
winget install --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705 --source winget
winget pin add --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705

# Build toolchain and shell (the justfile runs its scripts in pwsh); any SDK >= 10 builds, CI uses 10.0.x
winget install --exact --id Microsoft.DotNet.SDK.10 --source winget
winget install --exact --id Microsoft.PowerShell --source winget
winget install --exact --id Git.Git --source winget

# Host: Command Palette ships with PowerToys
winget install --exact --id Microsoft.PowerToys --source winget

# Deploy and publish: winapp (dev registration, MSIX), msstore (Store submissions)
winget install --exact --id Microsoft.WinAppCli --source winget
winget install --exact --id 9P53PC5S0PHJ --source msstore
```

## Daily loop

All commands are in the root `justfile` ([just](https://just.systems)); `just` lists them.

1. `just build` (Debug x64). If the running extension locks `bin/`: `just build <other dir>`.
2. `just test`, `just check`.
3. `just register` (uninstalls any installed copy first, keeping dev settings); if Command Palette is running it also reloads extensions via `x-cmdpal://reload` (the first request asks to confirm).
4. After a WindowsAppSDK, CsWinRT or packaging change: `just trim`, `just bundle`.

Before a release: `just versions`, `just pins`.

## Test

`UnityExtension.Tests` (xUnit) covers `ProjectParser` only. Check UI and commands by hand in
PowerToys; [PRODUCT.md](PRODUCT.md#current-behavior) lists the expected behavior.

## Debug

Attach to `UnityExtension.exe` after `just register`. Useful breakpoints: provider constructor,
`UnityExtensionPage.GetItems`, `OpenUnityCommand.Invoke`, `ProjectParser`, `EditorParser`. Watch the
VS Output window ("Debug").

## Code rules

- Trim/AOT-safe, no reflection unless an upstream API needs it; keep `sealed partial` where
  generators need it.
- CLSID: `[Guid]` in `UnityExtension.cs` equals both `ClassId` entries in `Package.appxmanifest`.
- Package versions only in `Directory.Packages.props`.
- `ProjectParser` swallows exceptions and returns an empty list.
- Hub data: the only write is deleting a `projects` row in `hub.db` via `HubDatabase`; never read
  `accounts.db`, never log raw project-record JSON.
