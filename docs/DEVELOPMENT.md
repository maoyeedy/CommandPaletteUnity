# Development Guide

## Build

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64
```

For packaged MSIX output (local dev — no thumbprint needed, `GenerateTemporaryStoreCertificate` in csproj handles it):

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|arm64" -p:SuppressSdkNoise=true
```

Trim / AOT notes:

- `IL2081` / `IL2104` from `ABI.Windows.Foundation.*`, `ABI.System.Collections.Generic.*`, `WinRT.Marshaler<...>` — known CsWinRT / Windows SDK projection warnings.
- Treated as non-fatal via `WarningsNotAsErrors` but still appear in build output.
- CsWinRT / Windows SDK projection noise suppressed by default for local publishes via `SuppressSdkNoise=true`.
- Use `-p:SuppressSdkNoise=false` to review full trim / AOT warning output.
- Avoid broad linker suppression for release validation; verify package loads + runs in PowerToys.

## Test

`UnityExtension.Tests` (xunit) covers `ProjectParser`: `dotnet test UnityExtension.Tests/UnityExtension.Tests.csproj -p:Platform=x64`. UI and command behavior is still verified manually in PowerToys. After a rebuild, reload Command Palette or restart PowerToys.

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
- `GenerateTemporaryStoreCertificate=True` in csproj for local dev; pass `PackageCertificateThumbprint` for Store-signing cert
- If `GenerateAppxPackageOnBuild=true`, do not force `WindowsPackageType=None`
- Local deployment: `Add-AppxPackage -Register` against debug `AppxManifest.xml`
- Prefer current Microsoft Learn packaging guidance over old repo scripts

## Dependency updates

NuGet first (affects compilation), then local tools, then pre-commit hooks. All package versions live in `Directory.Packages.props`.

```bash
dotnet package list --outdated --project UnityExtension/UnityExtension.csproj
dotnet package update <PackageId>@<version> --project UnityExtension/UnityExtension.csproj
dotnet tool update --local csharpier   # --local is required, else the global manifest is updated
dotnet tool update --local minver-cli
dotnet tool restore
pre-commit autoupdate && pre-commit run --all-files   # reject any hook rev downgrade
```

Rules:

- The extension runs inside PowerToys, so keep `Microsoft.CommandPalette.Extensions`, `Microsoft.WindowsAppSDK`, `Microsoft.Windows.CsWinRT` and `Microsoft.Windows.SDK.BuildTools` at or below the versions in [PowerToys' `Directory.Packages.props`](https://github.com/microsoft/PowerToys/blob/main/Directory.Packages.props). Do not use prerelease versions.
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT` must stay mutually compatible, or AOT/trim can break.
- `Microsoft.CommandPalette.Extensions` is date-versioned; bump only when targeting a newer PowerToys release.
- A new `Microsoft.Windows.SDK.BuildTools` base (e.g. `10.0.26100.x` to `10.0.27000.x`) also needs the `windows10.0.NNNNN.0` suffix of `TargetFramework` in `UnityExtension.csproj` and `MaxVersionTested` in `Package.appxmanifest` updated. Leave `MinVersion` and `Identity Version` alone.

Validate after updates:

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:SuppressSdkNoise=false --no-incremental   # when WindowsAppSDK or CsWinRT changed
dotnet tool run csharpier check .
```
