---
paths:
  - "vendor/**"
---

# Vendored PowerToys source map

`vendor/PowerToys/` is reference only, never a build input. Search it with `fd`/`rg` before falling back to `gh api`; every path in the table is materialized locally. Paths are relative to `vendor/PowerToys/src/modules/cmdpal/`.

> `vendor/PowerToys/` is a shallow (`--depth 1`) clone of `main` with exactly one commit: `git log -S`, `git blame` and `git log --before` all attribute every line to the current HEAD and cannot tell when a feature landed. Use `gh api repos/microsoft/PowerToys/commits?path=<path>` for history. To check a feature against the installed host, not `main`:
>
> 1. `(Get-AppxPackage Microsoft.CommandPalette).InstallLocation` -> `BuildInfo.xml`: `SemanticVersion=0.13.261105006` means built 2026-11-05, matching PowerToys release `v0.102.3101` (2026-11-06). Fetch it without switching the tree: `git -C vendor/PowerToys fetch --depth 1 origin refs/tags/v0.102.3101:refs/tags/v0.102.3101`, then `git -C vendor/PowerToys grep <pattern> v0.102.3101 -- src/modules/cmdpal`.
> 2. Custom Adaptive Card inputs are registered in the host binary: `rg -a --encoding utf-16le 'Input[.]CommandPalette[.]FilePath' "<InstallLocation>\Microsoft.CmdPal.UI.exe"`.
> 3. Compile-time APIs are gated by the pinned NuGet package DLL/winmd in `$env:USERPROFILE\.nuget\packages\microsoft.commandpalette.extensions\0.9.260303001`, not by either source tree.
>
> Verified 2026-11-25: `Input.CommandPalette.FilePath` / `FilePathList` exist in the installed 0.13.13096.0 host and at `v0.102.3101` -- they are not 0.14-only. The pinned Toolkit package lacks the `FilePathSetting` helpers, so hand-written Adaptive Card JSON is needed until `just pins` allows a package bump.

| Path | Read it for |
|---|---|
| `extensionsdk/Microsoft.CommandPalette.Extensions` | WinRT ABI in `Microsoft.CommandPalette.Extensions.idl`: `IExtension`, `ICommandItem`, `IListPage`, `IDetails`, `ICommandResult`. |
| `extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit` | Concrete `ListPage`/`Details`/`CommandResult`, `OpenUrlCommand`, `CopyTextCommand`/`CopyPathCommand`, `IconHelpers`, `KeyChordHelpers`, `ShellHelpers.OpenInShell`. |
| `ExtensionTemplate/TemplateCmdPalExtension` | `Program.cs` COM bootstrap, `IExtension.GetProvider`, `TopLevelCommands()`, `Package.appxmanifest` registration. |
| `ext/SamplePagesExtension` | Page-pattern catalog `SamplesListPage.cs`, sections in `Pages/SectionsPages/`, details in `SampleListPageWithDetails.cs`, grids in `SampleGridsListPage.cs`. |
| `ext/Microsoft.CmdPal.Ext.Apps` | `AllAppsPage`/`AppListItem`/`AppCommand`, `Win32Program.GetCommands()` -> `ToAppItem()` -> `MoreCommands`, icons in `Icons.cs` + `AppxIconLoader`, confirm/toast patterns. |
| `Microsoft.CmdPal.Common` | `Helpers/WellKnownKeyChords.cs`, `PathHelper`, `CommandLineParser`, `Text/FuzzyMatcherProvider`, logging, sanitization, extension gallery. |
| `Microsoft.CmdPal.UI` | `Controls/DetailsPane.xaml` (link/metadata templates), `Controls/AdaptiveCards/` (custom inputs/pickers), and `Helpers/MarkdownImageProviders/` (`file:`/`ms-appx:`/`data:` images). |
| `Microsoft.CmdPal.UI.ViewModels` | `DetailsViewModel.cs` (metadata rows) and `DetailsLinkViewModel.cs` (`file:` links via `NavigateCommand` -> `ShellHelpers.OpenInShell`). |
