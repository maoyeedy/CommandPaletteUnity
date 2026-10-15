---
name: bump-deps
description: >-
  Update third-party dependencies of the Unity Command Palette extension: NuGet packages in Directory.Packages.props, dotnet local tools in dotnet-tools.json, pre-commit hook revisions, and Windows SDK targets. Use for dependency, package, tool or hook upgrades. Not for the app's own version number (use release).
---

# Bump dependencies

Order matters: NuGet first (affects compilation), then tools and hooks, then validate. Never touch `<Version>`, `<AssemblyVersion>`, `<FileVersion>` or the manifest `Identity Version` (that is `release`), nor `rollForward` values in `dotnet-tools.json`.

## 1. NuGet

Survey first:

```
dotnet package list --outdated --project UnityExtension/UnityExtension.csproj
```

Apply capped bumps one package at a time with the built-in SDK 10 verb, which accepts an exact version: `dotnet package update <Id>@<version> --project UnityExtension/UnityExtension.csproj`. It rewrites `Directory.Packages.props` formatting (drops blank lines and the trailing newline), so check `git diff` and restore them. `dotnet-outdated` (global tool, `--upgrade --version-lock Minor --exclude <Id>`) is the alternative for bulk upgrades; it does not know about PowerToys' pins, so never accept its result unreviewed.

**Cap shared packages at PowerToys' pins.** The extension runs inside the PowerToys host, so never exceed what it ships. Fetch the pins:

```
gh api repos/microsoft/PowerToys/contents/Directory.Packages.props --jq .content | base64 -d
```

Compare `Microsoft.CommandPalette.Extensions`, `Microsoft.WindowsAppSDK`, `Microsoft.Windows.CsWinRT`, `Microsoft.Windows.SDK.BuildTools` and `Microsoft.Windows.CsWin32`. If the latest NuGet version is above PowerToys' pin, use the pin. Do not move to prerelease or experimental versions.

Package notes:

- `Microsoft.CommandPalette.Extensions` is date-versioned; bump only to a version PowerToys uses. After a bump, look up API changes (Learn MCP `microsoft_docs_search`, Context7, PowerToys release notes), build, and re-check the `sealed partial` classes (`InvokableCommand`, `ListPage`, `IExtension` host), `CommandResult` usage and the CLSID link to `Package.appxmanifest`.
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.CsWinRT` must stay mutually compatible or AOT/trim can break.
- `Microsoft.Windows.SDK.BuildTools` moving to a new SDK base (e.g. `10.0.26100.x` to `10.0.27000.x`) also requires step 4.
- `Shmuelie.WinRTServer`: do not bump a major without testing in PowerToys.

## 2. Local tools

`--local` is required; without it the global manifest is updated instead.

```
dotnet tool update --local csharpier
dotnet tool update --local minver-cli
dotnet tool restore
```

Add any tools that appear in `dotnet-tools.json`.

## 3. Pre-commit hooks

```
pre-commit autoupdate
```

Check the diff: reject any change that lowers a `rev` (autoupdate has proposed a gitleaks downgrade before; `git checkout -- .pre-commit-config.yaml` if so). Then `pre-commit run --all-files`; the end-of-file hook may rewrite `dotnet-tools.json`, so run it twice until it passes.

## 4. Windows SDK targets (only for a new SDK base)

- `UnityExtension/UnityExtension.csproj`: update the `windows10.0.NNNNN.0` suffix of `TargetFramework`; leave `SupportedOSPlatformVersion` and `TargetPlatformMinVersion` alone.
- `UnityExtension/Package.appxmanifest`: set `MaxVersionTested` on each `TargetDeviceFamily`; leave `MinVersion` alone.

## 5. Validate

```
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:SuppressSdkNoise=false --no-incremental
dotnet tool run csharpier check .
```

Run the Release build whenever WindowsAppSDK or CsWinRT changed; it is what catches trim/AOT regressions. If an upgrade breaks the build, pin one step back and say why. Loading the result in PowerToys (`deploy` skill) is the only check that proves runtime compatibility; say when it was not done.

## Report

Per change: name, old to new, and why anything was held back (PowerToys pin, prerelease). Then build results and warnings worth knowing.
