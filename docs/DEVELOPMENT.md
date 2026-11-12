# Development Guide

## Machine setup

Core dev tools via winget, then `just doctor`. Windows App Runtime, VCLibs and UI.Xaml framework packages arrive as MSIX dependencies; do not install them by hand.

```powershell
# Windows SDK: keep in step with Microsoft.Windows.SDK.BuildTools in Directory.Packages.props; the pin stops `winget upgrade --all` from moving it
winget install --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705 --source winget
winget pin add --exact --id Microsoft.WindowsSDK.10.0.26100 --version 10.0.26100.7705

# Build toolchain and shell (the justfile runs its scripts in pwsh)
winget install --exact --id Microsoft.DotNet.SDK.10 --source winget
winget install --exact --id Microsoft.PowerShell --source winget
winget install --exact --id Git.Git --source winget

# Host: Command Palette ships with PowerToys
winget install --exact --id Microsoft.PowerToys --source winget

# Deploy and publish: winapp (dev registration, MSIX), msstore (Store submissions)
winget install --exact --id Microsoft.WinAppCli --source winget
winget install --exact --id 9P53PC5S0PHJ --source msstore
```

When `Microsoft.Windows.SDK.BuildTools` moves to a new version, update both SDK lines (`winget pin remove` the old pin first). Remove side-by-side .NET SDKs nothing pins to, so `dotnet --list-sdks` stays clean.

## Build

Commands live in the root `justfile` ([just](https://just.systems)); run `just` to list them. On a new machine run `just doctor` first.

- `just build` (Debug x64), `just trim` (Release with full trim/AOT warnings), `just bundle` (unsigned x64+arm64 MSIX bundle, as CI), `just clean`
- `just test`, `just check` (csharpier + pre-commit), `just versions`, `just pins`
- `just register` registers the Debug build in Command Palette

A plain signed `dotnet build ... -p:GenerateAppxPackageOnBuild=true` fails with `APPX0101` without a certificate thumbprint; build unsigned and sign afterwards.

Trim / AOT notes:

- `IL2081` / `IL2104` from `ABI.Windows.Foundation.*`, `ABI.System.Collections.Generic.*`, `WinRT.Marshaler<...>` — known CsWinRT / Windows SDK projection warnings.
- Treated as non-fatal via `WarningsNotAsErrors` but still appear in build output.
- CsWinRT / Windows SDK projection noise suppressed by default for local publishes via `SuppressSdkNoise=true`.
- Use `-p:SuppressSdkNoise=false` to review full trim / AOT warning output.
- Avoid broad linker suppression for release validation; verify package loads + runs in PowerToys.

## Test

`UnityExtension.Tests` (xunit) covers `ProjectParser`: `just test`. UI and command behavior is still verified manually in PowerToys. After a rebuild, reload Command Palette or restart PowerToys.

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
- Package identity (`Name`, `Publisher`) must match Partner Center reserved values
- Bundle builds need either `-p:AppxPackageSigningEnabled=false` (then sign the bundle) or `-p:PackageCertificateThumbprint=<thumb>`; `GenerateTemporaryStoreCertificate` alone is not enough
- `Properties/PublishProfiles/win-x64.pubxml` and `win-arm64.pubxml` must stay in sync (e.g. both set or both omit `PublishReadyToRun`)
- If `GenerateAppxPackageOnBuild=true`, do not force `WindowsPackageType=None`
- Local deployment: `Add-AppxPackage -Register` against debug `AppxManifest.xml`
- Prefer current Microsoft Learn packaging guidance over old repo scripts

## Dependency updates

NuGet first (affects compilation), then local tools, then pre-commit hooks. All package versions live in `Directory.Packages.props`.

```bash
dotnet package list --outdated --project UnityExtension/UnityExtension.csproj
dotnet package update <PackageId>@<version> --project UnityExtension/UnityExtension.csproj
dotnet tool update --local csharpier   # --local is required, else the global manifest is updated
dotnet tool update --local dotnet-outdated-tool
dotnet tool restore
pre-commit autoupdate && pre-commit run --all-files   # reject any hook rev downgrade
```

Rules:

- The extension runs inside PowerToys (`just pins` checks this), so keep `Microsoft.CommandPalette.Extensions`, `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT` (runtime-shared) at or below the versions in [PowerToys' `Directory.Packages.props`](https://github.com/microsoft/PowerToys/blob/main/Directory.Packages.props). Do not use prerelease versions.
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT` must stay mutually compatible, or AOT/trim can break.
- `Microsoft.CommandPalette.Extensions` is date-versioned; bump only when targeting a newer PowerToys release.
- A new `Microsoft.Windows.SDK.BuildTools` base (e.g. `10.0.26100.x` to `10.0.27000.x`) also needs the `windows10.0.NNNNN.0` suffix of `TargetFramework` in `UnityExtension.csproj` and `MaxVersionTested` in `Package.appxmanifest` updated. Leave `MinVersion` and `Identity Version` alone.

Validate after updates: `just build`, `just check`, `just pins`, and `just trim` when WindowsAppSDK or CsWinRT changed.
