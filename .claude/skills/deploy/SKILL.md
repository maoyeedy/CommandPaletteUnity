---
name: deploy
description: >-
  Build the Unity Command Palette extension and install it into Command Palette so local changes show up, either as a fast debug registration or a signed release MSIX bundle. Use when the user wants to deploy, reinstall, sideload or test the extension in Command Palette, or when the extension is not reflecting recent edits.
---

# Deploy

Two modes. Default to **debug** for iteration; use **release** only when signing or packaging is mentioned. Local debug `just register` is routine after extension code changes -- run it without asking and report whether it succeeded, failed, or Command Palette was not running. Run from the repo root. For multi-line PowerShell use `pwsh -NoProfile -Command - <<'PWSH' ... PWSH` (never inside bash double quotes, `$` and backticks get expanded).

## 1. Stop the running extension

Locked files make register/install fail. For a release install, `Add-AppxPackage ... -ForceApplicationShutdown` closes the running extension itself. For a debug register, nothing: `just register` uninstalls the installed copy, which stops the extension. Otherwise use `Stop-Process -Force` only with the user's OK (project rule: never kill the running extension just to compile-check, use `-p:OutDir=<other dir>/` instead).

## 2. Build

Debug: skip to step 3, `just register` builds.

Release bundle: `just bundle` (unsigned, mirrors CI). Output (repo root): `AppPackages/UnityExtension_<version>.0_Test/UnityExtension_<version>.0_x64_arm64.msixbundle`. Build, signing and packaging errors are in `docs/TROUBLESHOOTING.md` and `docs/PUBLISHING.md`; for Store-signed builds see "Store submission" in PUBLISHING, with private identity values in `../store-identity.md` outside the repo.

Stop and report if the build fails; do not deploy a stale output.

## 3. Install

**Debug.** `just register` (MSIX debug build, Assets copy, `Add-AppxPackage -Register`). It first removes any installed copy: a dev registration with `-PreserveApplicationData` (settings kept), a release install outright (its settings reset). It then reloads extensions via `x-cmdpal://reload` if Command Palette is running.

**Release.** Follow "Signing" and "Install for testing" in `docs/PUBLISHING.md` (`#signing`, `#install-for-testing`). Use `scripts/Sign-LocalMsix.ps1` (read its `param()` block; pass `-BundlePath` and `-Subject` equal to the manifest `Publisher`). Trusting the cert and verifying the signature need an **elevated** shell, which an agent session is not; hand the user the exact admin command. Confirm with `Get-AppxPackage` (new version, `SignatureKind` Developer) and that `UnityExtension.exe` is running from `WindowsApps`.

## 4. Reload and verify

Command Palette runs standalone; PowerToys need not be open. After a release install, reload with `Start-Process x-cmdpal://reload` (the first call asks to confirm; choose "Always allow" to stop being asked). If Command Palette is not running, start it with `Start-Process 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'`. A duplicate "Unity Projects" entry after register is CmdPal's provider cache, not a second install; a Reload clears it (see `docs/TROUBLESHOOTING.md#changes-not-showing`). Open Command Palette and confirm the entry lists projects. If it does not load, check manifest registration and CLSID before anything else (see `docs/TROUBLESHOOTING.md`).

Debug builds register as `...Debug` with a "(Dev)" display name; `*CommandPalette-Unity*` matches both.

## Registration problems

- `Add-AppxPackage -Register` fails with `0x800704EC`: Developer Mode is off (Settings > Privacy & security > For developers). Ask the user to enable it.
- Start menu / Command Palette entry does nothing: a stale registration points at deleted files (for example after `bin`/`obj` cleanup). `winapp unregister --prune` removes every dev registration whose files are gone; then register again.
- Other install, cert or identity errors: use the `winapp-troubleshoot` skill.
