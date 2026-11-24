# Unity Hub and editor install locations

Hub 4 is an MSIX package. `hubInfo.json` (in [config-and-logs.md](config-and-logs.md)) gives the resolved
`Unity Hub.exe` path; the `unityhub://` protocol registration is in [protocol.md](protocol.md).
Paths use `<version>` and `<install dir>` placeholders.

## Install-related JSON files and folders

| File | Shape | Role |
|---|---|---|
| `editors-v2.json` | `{"schema_version":"2","data":[...]}` | Editors added with "Locate". Hub-installed editors are not listed |
| `excludedEditorFolders.json` | `{"schema_version":"1","data":[...]}` | Editors removed from Installs |
| `installedEditorBinaryMetadata.json` | `{"<Unity.exe>":{"architecture","size","mtimeMs","updatedAt"}}` | Architecture cache. Can be stale |
| `dismissedEditorWarningLabels.json` | `["<version>"]` | Dismissed editor warnings |
| `secondaryInstallPath.json` | `""` or `"<install dir>"` | Editor install folder. `""` = `%ProgramFiles%\Unity\Hub\Editor` |
| `secondaryDownloadLocation.json` | `{"path":"..."}` | Download folder. Missing = `%APPDATA%\UnityHub\downloads` |
| `modulesPreference.json` | `{"selected":[...],"deselected":[...]}` | Default modules |
| `downloads\` | | `UnitySetup64-<version>.exe`, `UnitySetup-<Module>-Support-for-Editor-<version>.exe` |

## Installed editors: discovery and registry

Hub-installed editors are not listed in a file: Hub scans the install folder
(`secondaryInstallPath.json`) for `<version>\Editor\Unity.exe`. Log:
`<n> editor install(s) discovered by the engine sidecar in path: <dir>`.
`editors-v2.json` holds editors added with "Locate" and can be
`{"schema_version":"2","data":[]}` even with editors installed.

The extension reads `HKCU`/`HKLM\SOFTWARE\Unity Technologies\Installer\<subkey>` (`Version`,
`Location x64`) and uses `<install path>\Editor\Unity.exe` only when that file exists
(`EditorParser`), matching versions exactly. On Windows, `just editors`
(scripts/Get-UnityEditors.ps1) prints that same set of usable editors and reports the stale entries
it skipped, without running the CLI or starting Unity.

## Related sources

- CLI view (`unity editors`, `editors install-path`): [../cli.md](../cli.md), reference only.
- Editor-written recent-projects registry key: [../community.md#extra-data-source](../community.md#extra-data-source).
- Installing a missing version through a URL: [protocol.md](protocol.md#editor-install-unityhubversionchangeset).
