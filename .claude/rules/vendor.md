---
paths:
  - "vendor/**"
---

# Vendored PowerToys source map

`vendor/PowerToys/` is reference only, never a build input. Search it with `fd`/`rg` before falling back to `gh api`; every path in the table is materialized locally. Paths are relative to `vendor/PowerToys/src/modules/cmdpal/`.

> `scripts/Init-Vendor.ps1` pins `vendor/PowerToys/` to the PowerToys release tag matching the installed app (the host ships with it), so this source is what the installed Command Palette runs. The checkout is shallow (`--depth 1`): `git log -S` / `git blame` cannot date a feature; use `gh api repos/microsoft/PowerToys/commits?path=<path>`. Compile-time APIs are gated by the pinned NuGet package in `Directory.Packages.props`, not by this source.

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
