# Troubleshooting Guide

## Extension not visible

Check PowerToys, Command Palette, package install, manifest registration, CLSID consistency.

## Changes not showing

Stale package or PowerToys needs reload / restart.

Local debug: rebuild with `-p:Platform=x64`, re-register with `Add-AppxPackage -Register`, restart PowerToys.

Release validation: remove the old package and install the new bundle as described in [`PUBLISHING.md`](PUBLISHING.md#install-for-testing).

If stale after install, reload Command Palette or restart PowerToys.

## Breakpoints not hit

Wrong process, stale output, or code paths not executing.

## No Unity projects found

`ProjectParser` may swallow exceptions. Confirm `%APPDATA%\UnityHub\projects-v1.json` exists + valid JSON.

## Unity does not open

Check Unity Editor path resolution + registry lookup.

## After dependency updates

Recheck restore, trim / AOT warnings, packaging, manifest behavior.

Known non-fatal warnings:

- `IL2081` / `IL2104` from CsWinRT / Windows SDK projection code safe to ignore if package installs + runs.
- `mspdbcmf.exe could not be found` means symbols package not generated; app bundle still valid.
- Local publish commands suppress trim warnings by default; use `-p:SuppressSdkNoise=false` to inspect.

If an outdated-package check suggests a newer `Microsoft.WindowsAppSDK`, treat it as a compatibility check, not an automatic upgrade; stay at or below PowerToys' version.

If Release packaging fails with `WindowsPackageType=None` + `GenerateAppxPackageOnBuild=true`, remove hard `None` for build path.

If packaging fails with `IL2104` / `NETSDK1144`, check trimming enabled for packaging build path, temporarily disable if needed.

## Build fails: file is locked

`MSB3021` / `MSB3027` "locked by: Unity Extension for Command Palette" means the extension is running inside Command Palette and holds `UnityExtension.exe`. It is not a code error.

Close or unregister the extension (`winapp unregister`) for a normal build, or compile-check into another folder: `dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64 -p:OutDir=<other dir>/`. `-p:UseAppHost=false` fails with `NETSDK1067`.
