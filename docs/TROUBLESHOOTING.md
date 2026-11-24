# Troubleshooting Guide

## Extension not visible

Check Command Palette (runs standalone; PowerToys need not be open), package install, manifest registration, CLSID consistency.

## Changes not showing

Stale package, or Command Palette has not reloaded extensions.

- Local debug: `just register` (reloads extensions itself). Manual reload: `Start-Process x-cmdpal://reload`,
  or `Reload` in Command Palette.
- Release validation: remove the old package and install the new bundle
  ([PUBLISHING.md](PUBLISHING.md#install-for-testing)).

## Breakpoints not hit

Wrong process, stale output, or code paths not executing.

## No Unity projects found

`ProjectParser` swallows exceptions. Hub 4: `%APPDATA%\UnityHub\hub.db` must open (a corrupt one
gives an empty list, no JSON fallback). Hub 3: `projects-v1.json` must be valid JSON. Source rules:
[PRODUCT.md](PRODUCT.md#scope-decisions).

## Unity does not open

Check `EditorParser`: the project's exact version must be under
`HKCU`/`HKLM\SOFTWARE\Unity Technologies\Installer`. Installing a missing editor through Unity Hub does not require restarting Command Palette: `EditorParser` re-scans the registry on miss.

## Build fails: file is locked

`MSB3021` / `MSB3027` "locked by: Unity Extension for Command Palette" means the extension is running
inside Command Palette and holds `UnityExtension.exe`. It is not a code error.

Re-register to get a normal build, or compile-check into another folder: `just build <other dir>`.
Never kill the running extension just to compile-check. `-p:UseAppHost=false` fails with `NETSDK1067`.

## Known warnings

- `IL2081` / `IL2104` from `ABI.Windows.Foundation.*`, `ABI.System.Collections.Generic.*`,
  `WinRT.Marshaler<...>`: CsWinRT / Windows SDK projection noise. Non-fatal (`WarningsNotAsErrors`);
  safe if the package installs and runs. Local builds hide it (`SuppressSdkNoise=true`); pass
  `-p:SuppressSdkNoise=false` (and `-p:TrimmerSingleWarn=false`) to review everything. Avoid broad
  linker suppression for release validation.
- `mspdbcmf.exe could not be found`: no symbols package; the bundle is still valid.

## Packaging errors

- `WindowsPackageType=None` with `GenerateAppxPackageOnBuild=true`: remove the hard `None`.
- `IL2104` / `NETSDK1144`: check trimming for the packaging build; disable temporarily if needed.
- `APPX0101` "A signing key is required": pass `-p:AppxPackageSigningEnabled=false` (sign afterwards)
  or `-p:PackageCertificateThumbprint=<thumb>`. See [PUBLISHING.md](PUBLISHING.md#package-build).
- `NETSDK1094` "Unable to optimize assemblies for performance": the two publish profiles disagree on
  `PublishReadyToRun`, so restore skipped one architecture's runtime pack. Make `win-x64.pubxml` and
  `win-arm64.pubxml` match, then `just clean` and `just bundle`. The ILLink crash (exit code
  `-1073741819`) showed up in the same state.
- Signing verification says "root certificate not trusted" or install fails: trust the cert in
  `Cert:\LocalMachine\TrustedPeople` from an elevated shell; `CurrentUser` is not enough.

## Release workflow errors

- `startup_failure` with zero jobs: an action is not allowed by the repo's Actions policy (Settings >
  Actions > General). Allow `microsoft/microsoft-store-apppublisher@*`. Such runs cannot be retried;
  push the tag again (see "Re-pointing a tag" in
  [PUBLISHING.md](PUBLISHING.md#github-releases-flow)).
- "No .msixbundle found" after a successful build: `PACKAGE_DIR` must be absolute; a relative
  `AppxPackageDir` is resolved against the project folder.
- `SignTool Error: ... root certificate which is not trusted` in "Verify MSIX bundle": the
  self-signed `.cer` must be imported into `LocalMachine\TrustedPeople` before `signtool verify`.
- A green run does not mean the update is live; `submission poll` returns while the submission is
  still in certification.

## Store publishing errors

- "Ingestion API can only update, delete, and commit submissions that are created through the API":
  a draft made in the Partner Center web UI blocks `msstore`. Delete or submit it in the portal first
  (CI would hang on the browser prompt).
- `msstore reconfigure` "Error while retrieving Organization": use the flag form
  (`--tenantId --sellerId --clientId --clientSecret`) instead of the interactive prompts. See
  [PUBLISHING.md](PUBLISHING.md#microsoft-store-updates-automated).
