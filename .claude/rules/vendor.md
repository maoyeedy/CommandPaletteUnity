---
paths:
  - "vendor/**"
---

# Vendored PowerToys source map

`vendor/PowerToys/` is reference only, never a build input. Search it with `fd`/`rg` before falling back to `gh api`; every path in the table is materialized locally.

| Path (under `vendor/PowerToys/src/modules/cmdpal/`) | Read it for |
|---|---|
| `extensionsdk/Microsoft.CommandPalette.Extensions` | WinRT API surface: `IExtension`, `ICommandItem`, `ListPage`, `Details`, `CommandResult`. |
| `extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit` | Helpers: `OpenUrlCommand`, `CopyTextCommand`/`CopyPathCommand`, `IconHelpers`, `KeyChordHelpers`. |
| `ExtensionTemplate/TemplateCmdPalExtension` | COM server bootstrap (`Program.cs`), `IExtension` wiring, `Package.appxmanifest` registration. |
| `ext/SamplePagesExtension` | Page patterns: `Section`, `DetailsLink`, `SampleListPageWithSections`, grid layouts. |
| `ext/Microsoft.CmdPal.Ext.Apps` | Built-in extension conventions: Search Apps icons and `Win32Program.GetCommands()`. |
| `Microsoft.CmdPal.Common` | Shared host conventions such as `WellKnownKeyChords.cs`. |
| `Microsoft.CmdPal.UI` | Host details-pane XAML (`DetailsPane.xaml`). |
| `Microsoft.CmdPal.UI.ViewModels` | Details-pane view models (`DetailsLinkViewModel.cs`), including `file:///` URI handling. |
