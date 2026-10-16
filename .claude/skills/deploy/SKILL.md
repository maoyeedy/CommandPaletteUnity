---
name: deploy
description: >-
  Build the Unity Command Palette extension and install it into PowerToys so local changes show up, either as a fast debug registration or a signed release MSIX bundle. Use when the user wants to deploy, reinstall, sideload or test the extension in Command Palette, or when the extension is not reflecting recent edits.
---

# Deploy

Two modes. Default to **debug** for iteration; use **release** only when signing or packaging is mentioned. Run from the repo root. For multi-line PowerShell use `pwsh -NoProfile -Command - <<'PWSH' ... PWSH` (never inside bash double quotes, `$` and backticks get expanded).

## 1. Stop PowerToys and the extension

Locked files make register/install fail. For a release install, `Add-AppxPackage ... -ForceApplicationShutdown` closes the running extension itself. For a debug register, close `PowerToys`, `Microsoft.CmdPal.UI` and `UnityExtension` gracefully first; use `Stop-Process -Force` only with the user's OK (project rule: never kill the running extension just to compile-check, use `-p:OutDir=<other dir>/` instead).

## 2. Build

Debug (needs `-p:WindowsPackageType=MSIX`, otherwise the csproj defaults to `None` outside VS and no `AppxManifest.xml` is generated):

```
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64 -p:WindowsPackageType=MSIX
```

Release bundle: use the restore + `--no-restore` unsigned command in `docs/PUBLISHING.md` ("Package build"); it mirrors CI. Output (repo root): `AppPackages/UnityExtension_<version>.0_Test/UnityExtension_<version>.0_x64_arm64.msixbundle`. A signed build without a thumbprint fails with `APPX0101`; `NETSDK1094` means the two `win-*.pubxml` profiles disagree (see `docs/TROUBLESHOOTING.md`). For Store-signed builds see "Store submission" in the same doc; the private identity values are in the git-ignored `docs/store-identity.md`.

Stop and report if the build fails; do not deploy a stale output.

## 3. Install

**Debug.** A release package of the same identity blocks `-Register` (error `0x80073CFB`), so remove any installed one first (`Get-AppxPackage *CommandPalette-Unity* | Remove-AppxPackage`). Copy `UnityExtension/Assets/*` into `Assets/` next to the output, then `Add-AppxPackage -Register <output>/AppxManifest.xml -ForceUpdateFromAnyVersion`. The manifest is under `UnityExtension/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/`; locate it with a glob rather than trusting the TFM.

**Release.** Sign the bundle with the git-ignored `scripts/sign.ps1` (read its `param()` block). Pass `-BundlePath` explicitly (auto-detect can pick a per-arch `.msix`) and `-Subject` equal to the manifest `Publisher` (the script's default `CN=Yidi Mao` fails its own check). Trusting the cert in `LocalMachine\TrustedPeople` and verifying the signature need an **elevated** shell; `-TrustStore CurrentUser` signs but then fails verification ("root certificate not trusted"). An agent session is not elevated, so hand the user the exact command to run in an admin terminal. Then `Get-AppxPackage *CommandPalette-Unity* | Remove-AppxPackage` and `Add-AppxPackage -Path <bundle> -ForceApplicationShutdown`; confirm with `Get-AppxPackage` (new version, `SignatureKind` Developer) and that `UnityExtension.exe` is running from `WindowsApps`.

## 4. Restart and verify

Start PowerToys (`shell:AppsFolder\Microsoft.PowerToysWin32`), open Command Palette and confirm the "Unity Projects" entry lists projects. If it does not load, check manifest registration and CLSID before anything else (see `docs/TROUBLESHOOTING.md`).

Debug builds register as `...Debug` with a "(Dev)" display name; `*CommandPalette-Unity*` matches both.
