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

```bash
dotnet test -p:Platform=x64
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
- Package identity (`Name`, `Publisher`) must match Partner Center reserved values
- `GenerateTemporaryStoreCertificate=True` in csproj for local dev; pass `PackageCertificateThumbprint` for Store-signing cert
- If `GenerateAppxPackageOnBuild=true`, do not force `WindowsPackageType=None`
- Local deployment: `Add-AppxPackage -Register` against debug `AppxManifest.xml`
- Prefer current Microsoft Learn packaging guidance over old repo scripts

## Dependency updates

Update in order — NuGet first (highest-impact, affects compilation and SDK targets), then local tools, then pre-commit hooks.

### NuGet packages

All version pins live in `Directory.Packages.props` (central package management).

```bash
dotnet-outdated.exe UnityExtension/UnityExtension.csproj --upgrade --pre-release Auto --include-auto-references --include-up-to-date
```

Review the diff before moving on: `git diff -- Directory.Packages.props`

Package-specific notes:

| Package | Note |
|---------|------|
| `Microsoft.CommandPalette.Extensions` | Date-versioned, tied to installed PowerToys build. Only bump when targeting a newer PowerToys release. |
| `Microsoft.WindowsAppSDK` | Major bumps may require WinUI/packaging changes. Check release notes. |
| `Microsoft.Windows.CsWinRT` | Must stay compatible with `Microsoft.WindowsAppSDK`. Bumping either without the other can break AOT/trim. |
| `Microsoft.Windows.SDK.BuildTools` | New SDK major (e.g. `10.0.26100.x` → `10.0.27000.x`) triggers SDK target updates — see below. |
| `StyleCop.Analyzers` | Pre-release track; `--pre-release Auto` picks up new betas automatically. |

### Dotnet local tools

`--local` is required. Without it, `dotnet tool update` targets the global manifest, not `dotnet-tools.json`.

```bash
dotnet tool update --local csharpier
dotnet tool update --local minver-cli
dotnet tool restore
```

Adapt the list if new tools appear in `dotnet-tools.json`.

### Pre-commit hooks

```bash
pre-commit autoupdate
pre-commit run --all-files
```

Inspect the diff — major hook version bumps (especially gitleaks) can introduce breaking config changes.

### SDK targets (only when SDK.BuildTools major changes)

Only needed when `Microsoft.Windows.SDK.BuildTools` moves to a new Windows SDK major (e.g. `10.0.26100.x` → `10.0.27000.x`). Minor/patch bumps within the same base: no action needed.

- **`UnityExtension/UnityExtension.csproj`**: update the `windows10.0.NNNNN.0` suffix in `TargetFramework`. Leave `SupportedOSPlatformVersion` alone.
- **`UnityExtension/Package.appxmanifest`**: update `MaxVersionTested` on each `<TargetDeviceFamily>`. Leave `MinVersion` and `Identity Version` alone.

### Validation

Always run a debug build after updates:

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
```

If `Microsoft.WindowsAppSDK` or `Microsoft.Windows.CsWinRT` changed, also run a release build to catch trim/AOT regressions:

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:SuppressSdkNoise=false
```
