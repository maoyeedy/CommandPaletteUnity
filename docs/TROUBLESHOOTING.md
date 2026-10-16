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

## Bundle build errors

- `APPX0101` "A signing key is required": pass `-p:AppxPackageSigningEnabled=false` (sign afterwards) or `-p:PackageCertificateThumbprint=<thumb>`. See "Package build" in [`PUBLISHING.md`](PUBLISHING.md#package-build).
- `NETSDK1094` "Unable to optimize assemblies for performance": the two publish profiles disagree on `PublishReadyToRun`, so restore skipped one architecture's runtime pack. Make `win-x64.pubxml` and `win-arm64.pubxml` match, delete `bin`/`obj`/`AppPackages`, then restore and build again. The ILLink crash (exit code `-1073741819`) showed up in the same state.
- Signing verification says "root certificate not trusted" or install fails: trust the cert in `Cert:\LocalMachine\TrustedPeople` from an elevated shell; `CurrentUser` is not enough.

## Store publishing errors

- "Ingestion API can only update, delete, and commit submissions that are created through the API": a draft made in the Partner Center web UI blocks `msstore`. Delete or submit it in the portal first (CI would hang on the browser prompt).
- `msstore reconfigure` "Error while retrieving Organization": use the flag form (`--tenantId --sellerId --clientId --clientSecret`) instead of the interactive prompts. See [`PUBLISHING.md`](PUBLISHING.md#microsoft-store-updates-automated).

## Build fails: file is locked

`MSB3021` / `MSB3027` "locked by: Unity Extension for Command Palette" means the extension is running inside Command Palette and holds `UnityExtension.exe`. It is not a code error.

Close or unregister the extension (`winapp unregister`) for a normal build, or compile-check into another folder: `dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64 -p:OutDir=<other dir>/`. `-p:UseAppHost=false` fails with `NETSDK1067`.
