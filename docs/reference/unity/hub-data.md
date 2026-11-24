# Unity Hub 4 data folder (`%APPDATA%\UnityHub`)

Hub runs SQLite in WAL mode. To read while Hub runs, open read-only (or copy `.db`, `-wal`, `-shm`
together). Files marked *legacy* came from Hub 3 and are no longer the source of truth.

## `hub.db`: project registry

```sql
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
-- ('projects_json_migrated', '1') after the one-time import of projects-v1.json
CREATE TABLE projects (
  path       TEXT PRIMARY KEY,  -- project folder, Windows path
  data       TEXT NOT NULL,     -- JSON object, fields below
  updated_at INTEGER NOT NULL   -- Unix ms of the last row write
);
```

Behavior:

- One row per project in the Projects list.
- UI edits (favorite, tags, editor version, platform) write the row within seconds.
- **Remove from list** deletes the row and appends the path to `excludedProjectFolders.json`.
  `unity projects remove` deletes only the row; the extension does the same.
- Hub re-reads project folders on demand and rewrites changed rows. Log line:
  `project refresh from disk: <n> row(s) rewritten`.

## Running Hub

Hub is single-instance; the process is `Unity Hub.exe`, and `hubInfo.json` `executablePath` gives its
full path. While it runs, SQLite uses WAL mode, so `hub.db-wal` and `hub.db-shm` exist and
`logs\hub-gpui-running-<pid>-<id>.json` is written. `hubInfo.json` is rewritten on each launch.

The extension treats `hub.db-wal` existence as the running signal: without it the db file is
complete and read with `immutable=1` (no side files); with it, `readonly_shm=1` maps the existing
`-shm` read-only (`HubDatabase.ReadOnlyUri`). A normal start logs
`deeplink: argv carries no unityhub:// link`.

### `data` fields

"Usually" = present for projects opened at least once in a recent editor.

| Field | Presence | Type | Meaning |
|---|---|---|---|
| `title` | always | string | Display name. Folder name unless renamed |
| `hasCustomDisplayName` | usually | bool | `title` was renamed in Hub |
| `path` | always | string | Same as the row key |
| `containingFolderPath` | usually | string | Parent folder |
| `version` | always | string | Editor version, e.g. `6000.0.1f1` |
| `changeset` | always | string | Editor revision hash, may be `""` |
| `architecture` | always | string | `x86_64` or `arm64` |
| `isCustomEditor` | always | bool | Editor set to a path outside Hub's installs |
| `isFavorite` | always | bool | Star |
| `lastModified` | always | Unix ms or **`null`** | `null` if never opened |
| `sourceFingerprint` | usually | string | mtime/size list of project files for change detection. Hub 4 suffix `\|hub-gpui-derive:<n>;spn:false;usn:0`, Hub 3 suffix `\|spn:false;eds:` |
| `renderPipeline` | usually | string | `BuiltIn`, `URP`, `HDRP`, ... |
| `buildTarget` | often | string | `StandaloneWindows64`, `Android`, ... Missing = current platform |
| `cloudEnabled` | usually | bool | |
| `localProjectId` | usually | 32 hex | **Not unique**: a copied project keeps the id |
| `cloudProjectId` | if linked | GUID | Unity Cloud project |
| `projectName` | if linked | string | Unity Cloud project name |
| `organizationId` | if linked | string | Unity organization slug |
| `genesisOrgId` | sometimes | string | |
| `vcsProvider` | if Git remote known | string | `github`, `gitlab` |
| `organizationName` | if Git remote known | string | **Git** owner (not the Unity org) |
| `repositoryName` | if Git remote known | string | |
| `vcsConfigurationPath` | if Git | string | `<repo>\.git\config`. Can be in a parent folder |
| `projectPathInsideRepository` | if subfolder | string | Project path relative to repo root |
| `tags` | if any | string[] | **Hub 4 only.** Each up to 50 chars |

Computed live, not stored: current branch, folder size, remote repo size, Git account, warnings such
as "Missing Editor version".

## Other databases

| File | Tables | Use |
|---|---|---|
| `install-state\install-state.db` | `downloads` (uid, version, arch, modules_json, state `queued/downloading/complete/failed/cancelled`, bytes_total, bytes_done, pid, timestamps, error), `installs` (at most one row, phase `queued/installing`), `file_claims`, `schema_meta`. `downloads` and `installs` have `writer_kind` (`hub`, `hub-native`, `cli`): which process owns the row | Editor download/install queue, shared across processes. Empty when idle |
| `accounts.db` | `accounts`, `active_account`, `meta` | Signed-in Unity accounts. Personal data; tokens live in the OS credential store |

`DIPS`, `SharedStorage`, `IndexedDB`, `Local Storage`, `GPUCache` and similar are Chromium leftovers.

## JSON files

| File | Shape | Role |
|---|---|---|
| `projects-v1.json` | `{"schema_version":"v1","data":{"<path>":{...}}}` | *Legacy* project registry. Same fields as above, no `tags`. Still read by CLI 1.0.0 |
| `excludedProjectFolders.json` | `{"schema_version":"1","data":["<path>"]}` | Removed projects |
| `projectsInfo.json` | `{"<path>":{"cliArgs":"..."}}` | Extra editor arguments per project |
| `projectDir.json` | `{"directoryPath":"..."}` | Last folder used by Add / New project |
| `projectSortPreferences.json` | `{"sortBy":"lastModified","sortDirection":"ascending"}` | List sort |
| `projectTablePreferences.json` | `{"isFavorite":bool,"isVersionControlConnected":bool,"cloudServices":bool,"lastModified":bool,"version":bool,"tags":bool,"buildTarget":bool,"currentBranch":bool,"sizeInBytes":bool,"hideMissingProjects":bool}` | Visible columns and "hide missing projects" |
| `projectOrganizationSetting.json` | | Org filter |
| `editors-v2.json` | `{"schema_version":"2","data":[...]}` | Editors added with "Locate". Hub-installed editors are not listed |
| `excludedEditorFolders.json` | `{"schema_version":"1","data":[...]}` | Editors removed from Installs |
| `installedEditorBinaryMetadata.json` | `{"<Unity.exe>":{"architecture","size","mtimeMs","updatedAt"}}` | Arch cache. Can be stale |
| `dismissedEditorWarningLabels.json` | `["<version>"]` | Dismissed editor warnings |
| `secondaryInstallPath.json` | `""` or `"D:\\Unity\\Editor"` | Editor install folder. `""` = `%ProgramFiles%\Unity\Hub\Editor` |
| `secondaryDownloadLocation.json` | `{"path":"..."}` | Download folder. Missing = `%APPDATA%\UnityHub\downloads` |
| `modulesPreference.json` | `{"selected":[...],"deselected":[...]}` | Default modules |
| `hubInfo.json` | `{"version":"4.0.0","executablePath":"...\\Unity Hub.exe"}` | Installed Hub, rewritten each launch |
| `user-settings.json` | object | General settings (`minimizeToTray`, `themeSource`, `projects.showProductNames`, `deeplink.*`, `updater.releaseChannel`, `templates.*`, CLI consent flags). `unity config path` points here. Observed `deeplink.alwaysForwardToSingleEditor = true` (see [deeplinks.md](deeplinks.md)) |
| `hub-gpui-window-state.json` | `{x,y,width,height,maximized,fullscreen}` | Hub 4 window. `main-window.json` is *legacy* |
| `cachedFeatureFlags.json`, `cloudConfig.json`, `hubConfig.json` | | Remote flags and endpoints |
| `cli-plugin-registry.json`, `cli-skill-installs.json`, `cli-update-check.json` | | Unity CLI state |
| one-flag files | `hideHubOnEditorOpen`, `skipRemoveConfirmation`, `themeSetting`, `languageConfig`, `dismissedBanners`, ... | One preference each |

## Folders

| Folder | Content |
|---|---|
| `hub-gpui\` | Hub 4 caches: `downloadable-editors-cache.json` (releases by stream with download URLs), `live-platform-releases-cache.json`, `updates\` (staged Hub update MSIX) |
| `logs\` | JSON lines `{"level","time","pid","moduleName","msg"}`: `info-log.json` (+ rotations), `install-log.json`, `cli-log.json`, `broker-audit.json`, `hub-gpui-running-<pid>-<id>.json` while Hub runs |
| `downloads\` | Installers: `UnitySetup64-<version>.exe`, `UnitySetup-<Module>-Support-for-Editor-<version>.exe` |
| `Templates\` | Custom templates |

## Installed editors

Hub-installed editors are not listed in a file: Hub scans the install folder (`secondaryInstallPath.json`)
for `<version>\Editor\Unity.exe`. Log: `<n> editor install(s) discovered by the engine sidecar in
path: <dir>`. `editors-v2.json` holds editors added with "Locate" and can be
`{"schema_version":"2","data":[]}` even with editors installed. The extension reads
`HKCU`/`HKLM\SOFTWARE\Unity Technologies\Installer\<subkey>` (`Version`, `Location x64`) and uses
`<install path>\Editor\Unity.exe` (`EditorParser`), matching versions exactly.
