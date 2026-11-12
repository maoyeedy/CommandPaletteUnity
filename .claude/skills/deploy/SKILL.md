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

Debug: skip to step 3, `just register` builds.

Release bundle: `just bundle` (unsigned, mirrors CI). Output (repo root): `AppPackages/UnityExtension_<version>.0_Test/UnityExtension_<version>.0_x64_arm64.msixbundle`. A signed build without a thumbprint fails with `APPX0101`; `NETSDK1094` means the two `win-*.pubxml` profiles disagree (see `docs/TROUBLESHOOTING.md`). For Store-signed builds see "Store submission" in the same doc; the private identity values are in `../store-identity.md`, outside the repo.

Stop and report if the build fails; do not deploy a stale output.

## 3. Install

**Debug.** `just register` (MSIX debug build, Assets copy, `Add-AppxPackage -Register`). It refuses while a release package is installed (`0x80073CFB`) and prints the removal command; removing the user's release install needs their OK.

**Release.** Sign the bundle with the git-ignored `scripts/sign.ps1` (read its `param()` block). Pass `-BundlePath` explicitly (auto-detect can pick a per-arch `.msix`) and `-Subject` equal to the manifest `Publisher` (the script's default `CN=Yidi Mao` fails its own check). Trusting the cert in `LocalMachine\TrustedPeople` and verifying the signature need an **elevated** shell; `-TrustStore CurrentUser` signs but then fails verification ("root certificate not trusted"). An agent session is not elevated, so hand the user the exact command to run in an admin terminal. Then `Get-AppxPackage *CommandPalette-Unity* | Remove-AppxPackage` and `Add-AppxPackage -Path <bundle> -ForceApplicationShutdown`; confirm with `Get-AppxPackage` (new version, `SignatureKind` Developer) and that `UnityExtension.exe` is running from `WindowsApps`.

## 4. Restart and verify

Start PowerToys (`shell:AppsFolder\Microsoft.PowerToysWin32`), open Command Palette and confirm the "Unity Projects" entry lists projects. If it does not load, check manifest registration and CLSID before anything else (see `docs/TROUBLESHOOTING.md`).

Debug builds register as `...Debug` with a "(Dev)" display name; `*CommandPalette-Unity*` matches both.

## Registration problems

- `Add-AppxPackage -Register` fails with `0x800704EC`: Developer Mode is off (Settings > Privacy & security > For developers). Ask the user to enable it.
- Start menu / Command Palette entry does nothing: a stale registration points at deleted files (for example after `bin`/`obj` cleanup). `winapp unregister --prune` removes every dev registration whose files are gone; then register again.
- Other install, cert or identity errors: use the `winapp-troubleshoot` skill.
