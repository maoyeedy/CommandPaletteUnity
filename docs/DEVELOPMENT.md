# Development Guide

## Build

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64
```

Trim / AOT notes:

- `IL2081` / `IL2104` from `ABI.Windows.Foundation.*`, `ABI.System.Collections.Generic.*`, `WinRT.Marshaler<...>` — known CsWinRT / Windows SDK projection warnings.
- Treated as non-fatal via `WarningsNotAsErrors` but still appear in build output.
- CsWinRT / Windows SDK projection noise suppressed by default for local publishes via `SuppressSdkNoise=true`.
- Use `-p:SuppressSdkNoise=false` to review full trim / AOT warning output.
- Avoid broad linker suppression for release validation; verify package loads + runs in PowerToys.

## Test

```bash
dotnet test -p:Platform=x64
```

For dependency updates, prefer:

```bash
dotnet-outdated.exe UnityExtension/UnityExtension.csproj --upgrade --pre-release Auto --include-auto-references --include-up-to-date
```

After rebuild, reload Command Palette or restart PowerToys.

## Debug

Put breakpoints in:

- command provider initialization
- `GetItems`
- command invocation paths
- project parsing
- registry lookup
- process launch helpers

Watch VS Output window, use temp logging if needed.

## Packaging reminders

- Keep `UnityExtension.cs` + `Package.appxmanifest` aligned
- CLSID must match in COM server + app extension registration
- Use `dotnet publish` or `dotnet build ... -p:GenerateAppxPackageOnBuild=true` for packaged output
- If `GenerateAppxPackageOnBuild=true`, do not force `WindowsPackageType=None`
- Local deployment: `Add-AppxPackage -Register` against debug `AppxManifest.xml`
- Prefer current Microsoft Learn packaging guidance over old repo scripts

## Dependency updates

```bash
dotnet-outdated.exe UnityExtension/UnityExtension.csproj --upgrade --pre-release Auto --include-auto-references --include-up-to-date
```

Update only what needed, re-test in PowerToys. Release packaging may surface trim warnings (`IL2104` / `NETSDK1144`); verify trimming should stay enabled.
