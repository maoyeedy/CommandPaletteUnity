# Dependencies

How to update NuGet packages, local tools, the git hook tools and the Windows SDK. The `bump-deps`
skill follows this file.

## Order

NuGet first (affects compilation), then local tools, then the hook tool pins. All package versions
live in `Directory.Packages.props`.

```bash
dotnet package list --outdated --project UnityExtension/UnityExtension.csproj
dotnet package update <PackageId>@<version> --project UnityExtension/UnityExtension.csproj
dotnet tool update --local csharpier   # --local is required, else the global manifest is updated
dotnet tool update --local dotnet-outdated-tool
dotnet tool restore
# actionlint/gitleaks: bump the constants in .githooks/pins.ps1, then `just doctor` and `just check`
```

## Rules

- The extension runs inside PowerToys (`just pins` checks this). Keep
  `Microsoft.CommandPalette.Extensions`, `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT`
  (runtime-shared) at or below the versions in `Directory.Packages.props` of the
  [latest PowerToys release](https://github.com/microsoft/PowerToys/releases/latest), not PowerToys
  `main` (which runs ahead of what users have). No prerelease versions. The `PowerToys pins`
  workflow runs this check on PRs that touch packages, and weekly in strict mode, where it also
  fails when PowerToys has moved ahead (run `bump-deps`).
- An outdated-package hint for a newer `Microsoft.WindowsAppSDK` is a compatibility check, not an
  automatic upgrade.
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT` must stay mutually compatible, or
  AOT/trim can break.
- `Microsoft.CommandPalette.Extensions` is date-versioned; bump only when targeting a newer
  PowerToys release.

## Windows SDK

The winget SDK version in [DEVELOPMENT.md](DEVELOPMENT.md#machine-setup) follows
`Microsoft.Windows.SDK.BuildTools` in `Directory.Packages.props`. The winget pin stops
`winget upgrade --all` from moving it.

- When BuildTools moves: `winget pin remove` the old pin, then update both SDK lines.
- A new base (e.g. `10.0.26100.x` to `10.0.27000.x`) also needs the `windows10.0.NNNNN` suffix of
  `TargetFramework` (no trailing `.0`: csharp-ls 0.28 rewrites the TFM without it, and a mismatch
  silently drops every NuGet reference) in both csproj files and `win-x64.pubxml`, and `MaxVersionTested` in `Package.appxmanifest`.
  Leave `MinVersion` and `Identity Version` alone.
- Remove side-by-side .NET SDKs nothing pins to, so `dotnet --list-sdks` stays clean.

## Validate

`just build`, `just check`, `just pins`, and `just trim` when WindowsAppSDK or CsWinRT changed.
Warnings that may appear: [TROUBLESHOOTING.md](TROUBLESHOOTING.md#known-warnings).
