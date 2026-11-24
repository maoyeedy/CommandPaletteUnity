# Development Guide

The dev loop: setup, build, test, debug. Each other topic has one home:

| Topic | File |
|---|---|
| Scope, UX | [PRODUCT.md](PRODUCT.md) |
| Roadmap | [ROADMAP.md](ROADMAP.md) |
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

First checkout: `just hooks` once per clone. It sets `git config core.hooksPath .githooks`, which
activates the committed pre-commit and pre-push checks; `just doctor` verifies it.

```powershell
# Windows SDK: version and pin rules in DEPENDENCIES.md
winget install --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705 --source winget
winget pin add --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705

# Build toolchain and shell (the justfile runs its scripts in pwsh); any SDK >= 10 builds, CI uses 10.0.x
winget install --exact --id Microsoft.DotNet.SDK.10 --source winget
winget install --exact --id Microsoft.PowerShell --source winget
winget install --exact --id Git.Git --source winget

# Host: Command Palette ships with PowerToys but runs standalone
winget install --exact --id Microsoft.PowerToys --source winget

# Deploy and publish: winapp (dev registration, MSIX), msstore (Store submissions)
winget install --exact --id Microsoft.WinAppCli --source winget
winget install --exact --id 9P53PC5S0PHJ --source msstore
```

### Upstream source

Agents read the Command Palette SDK, Toolkit, extension template and sample pages from a sparse
local checkout in `vendor/PowerToys/` (gitignored), not through `gh api`. Run `just vendor` once per
clone; `just vendor-update` refreshes it. The repo URL and the four sparse paths live in
`scripts/Init-Vendor.ps1` -- add a repo there when agents start consulting another upstream source.

## Daily loop

All commands are in the root `justfile` ([just](https://just.systems)); `just` lists them.

1. `just build` (Debug x64). If the running extension locks `bin/`: `just build <other dir>`.
2. `just test`, `just check`.
3. `just register` (uninstalls any installed copy first, keeping dev settings but resetting a release install's settings); if Command Palette is running it also reloads extensions via `x-cmdpal://reload` (the first request asks to confirm).
4. After a WindowsAppSDK, CsWinRT or packaging change: `just trim`, `just bundle`.

Reference-doc helpers (Windows dev-only, read-only, no Unity CLI): `just editors` lists usable
editors from the installer registry, `just hubfacts` prints masked local Hub facts used in
`docs/reference/unity/`, and `just docs-check` verifies relative links and heading anchors after doc
edits. `docs-check` checks syntax only, not whether a link points at the right fact.

Before a release: `just versions`, `just pins`.

For register caveats and loading problems, see
[TROUBLESHOOTING.md](TROUBLESHOOTING.md#changes-not-showing).

## Test

`UnityExtension.Tests` (xUnit) covers `ProjectParser`, `HubDatabase`, `HubDataWatcher`, path shortening, and Hub install URI validation (the WAL test needs
`pwsh`). Check UI and commands by hand in Command Palette;
[PRODUCT.md](PRODUCT.md#current-behavior) lists the expected behavior.

## Settings page

The settings page is a separate window from the palette. `CommandResult.Confirm` is a `ContentDialog`
owned by the palette window, so it cannot show in settings; destructive settings actions confirm
inline by swapping `FormContent.TemplateJson` in `SubmitForm` (the host re-renders the card on a
property change). See `SettingsPage.cs`.

## Debug

Attach to `UnityExtension.exe` after `just register`. Useful breakpoints: provider constructor,
`UnityExtensionPage.GetItems`, `OpenUnityCommand.Invoke`, `ProjectParser`, `EditorParser`. Watch the
VS Output window ("Debug").

For settings, break in `SettingsPage.DangerZoneForm.SubmitForm` or `RemoveAllProjectsCommand.Invoke`;
they run in the same process as the palette. To check the UI by hand, open the extension's settings
page in Command Palette, not the palette runner: the runner never shows settings content.

## Code rules

- Trim/AOT-safe, no reflection unless an upstream API needs it; keep `sealed partial` where
  generators need it.
- CLSID: `[Guid]` in `UnityExtension.cs` equals both `ClassId` entries in `Package.appxmanifest`.
- Package versions only in `Directory.Packages.props`.
- `ProjectParser` swallows exceptions and returns an empty list.
- Hub data: the only writes delete `projects` rows in `hub.db` via `HubDatabase` (one or all);
  never read `accounts.db`, never log raw project-record JSON.
