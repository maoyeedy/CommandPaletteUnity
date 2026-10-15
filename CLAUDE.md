# CLAUDE.md

AI agent instructions for this repo. Verified against the tree on 2026-10-16.

## What this is

Command Palette (PowerToys) extension that lists Unity Hub projects and opens them in the Unity Editor.
Reads `%APPDATA%\UnityHub\projects-v1.json`. .NET 10 (`net10.0-windows10.0.26100.0`), MSIX-packaged, trimmed/AOT-compatible.
Store identity: `YidiMao.CommandPalette-Unity` (private values in git-ignored `docs/store-identity.md`).

## Layout

- `UnityExtension/` = C# project (`Commands/`, `Data/`, `Helpers/`, `Pages/`, `Program.cs`, `UnityExtension.cs`, `Package.appxmanifest`)
- `docs/` = DEVELOPMENT, PUBLISHING, TROUBLESHOOTING (+ local-only, git-ignored `store-identity.md`); screenshots in `docs/assets/`
- `.github/workflows/release-msix.yml` = tag `v*` -> validate versions -> build -> sign -> GitHub release
- `Directory.Packages.props` = all package versions; `dotnet-tools.json` = csharpier, minver-cli; `.pre-commit-config.yaml` = hooks
- No Markdown under `UnityExtension/`; contributor guidance -> `CONTRIBUTING.md`; user docs -> `docs/`
- Local-only (git-ignored, do not commit): `.claude/settings.local.json`, `docs/store-identity.md`, `scripts/` (`sign.ps1`, `pfx.ps1`, Unity path helpers)
- No test project exists; verify manually in PowerToys
- Gallery metadata lives in `microsoft/CmdPal-Extensions` (`extensions/maoyeedy/cmdpal-unity-extension/`), not in this repo

## Docs and tooling (use before answering from memory)

- Context7: `/websites/learn_microsoft_en-us_windows_powertoys` (Command Palette SDK, packaging, publishing); `/microsoft/powertoys` for source
- Microsoft Learn MCP `microsoft-learn` (`https://learn.microsoft.com/api/mcp`, shared via `.mcp.json`): `microsoft_docs_search`, `microsoft_docs_fetch`, `microsoft_code_sample_search`
  - Fallback CLI: `npx @microsoft/learn-cli search|fetch|code-search`; `doctor` checks connectivity
- Own skills (`.claude/skills/`, editable, in scope for refactors): `deploy`, `release`, `bump-deps`
- Third-party skills (managed by `bunx skills`, tracked in `skills-lock.json`; never hand-edit or refactor): `microsoft-docs`, `microsoft-code-reference` (`MicrosoftDocs/mcp`); `publish-extension`, `add-extension-settings`, `add-dock-band` (official PowerToys extension template)
  - Add: `bunx skills add <owner/repo> -s <skill> -a claude-code --copy -y`; update: `bunx skills update -p -y`; list: `bunx skills list`; restore: `bunx skills experimental_install`
- Rule `.claude/rules/cmdpal-extension.md` (vendored from the PowerToys extension template, path-scoped to `UnityExtension/**/*.cs`; never hand-edit): Command Palette API reference and common mistakes
- CLIs (winget): `winapp` (Microsoft.WinAppCli: `run`/`unregister` dev registration, `cert`, `package`, `sign`, `ui` inspects the live Command Palette window) `actionlint` (also a pre-commit hook for `.github/workflows/`), and `msstore` (Store submissions; needs Partner Center credentials via `msstore reconfigure`, not configured)
- Recent Learn docs cover newer SDK surface (e.g. Dock bands); check them before assuming an API is missing

## Code rules

- Extension = COM-backed out-of-process server (`Program.cs` handles `-RegisterProcessAsComServer`, via Shmuelie.WinRTServer)
- Keep CLSID in sync: `[Guid]` in `UnityExtension.cs` <-> both `ClassId` entries in `Package.appxmanifest`
- Keep code trim-safe / AOT-compatible; preserve `sealed partial` where SDK/source generators require
- Central package versions only (`Directory.Packages.props`); no versions in `.csproj`
- Avoid reflection-heavy patterns unless upstream API requires
- Formatting is enforced by pre-commit (csharpier, actionlint, gitleaks), not by Claude hooks: hooks only fire on Edit/Write, not on shell bulk edits. Run `pre-commit run --all-files` after bulk changes

## Build and verify

```
dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:SuppressSdkNoise=false   # trim/AOT review
```

- Debug CLI build is green (0 warnings, 0 errors) as of this refresh
- CLI `Register` deploys need `-p:WindowsPackageType=MSIX`; MSIX bundle builds need `-p:GenerateAppxPackageOnBuild=true` (see `deploy` skill)
- `IL2081` / `IL2104` from CsWinRT projections are known and non-fatal
- After rebuild, reload Command Palette or restart PowerToys; test in the real host

## Gotchas

- `ProjectParser` swallows exceptions and may silently return an empty list
- Extension not loading: check manifest registration and CLSID first
- Breakpoints not hit: attach VS to the extension process, not the host shell
- `Microsoft.CommandPalette.Extensions` is date-versioned (currently 0.9.260303001); bump only for a newer PowerToys target
- `Microsoft.WindowsAppSDK` (2.2.0, matches PowerToys) and `Microsoft.Windows.CsWinRT` (2.2.0) must stay mutually compatible; keep <= PowerToys' pins
- Version sync: `Package.appxmanifest` Identity Version <-> `.csproj` `<Version>`/`<AssemblyVersion>`/`<FileVersion>` (currently 0.1.3 / 0.1.3.0); 4th segment `.0`. CI fails the tag build on mismatch
- Identity `Name`/`Publisher` must match Partner Center exactly
- After identity/name changes delete `bin`, `obj`, `AppPackages` before rebuilding
- Debug builds get `.Debug` package suffix and `(Dev)` display name; a release package blocks debug `-Register` (0x80073CFB), remove it first
- `Package.appxmanifest` `MaxVersionTested` is still 10.0.19041.0; only change via `bump-deps` Step 4

## Do not

- Do not trust README alone for publish/debug behavior; prefer current Learn docs
- Do not broaden package updates without checking compatibility and packaging impact
- Do not push tags, publish releases or submit to the Store without explicit user approval
