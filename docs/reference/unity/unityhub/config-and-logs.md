# Unity Hub config folder (`%APPDATA%\UnityHub`)

JSON preferences, logs and caches live here. The project database is in [registry.md](registry.md),
install locations in [install-locations.md](install-locations.md), and `unityhub://` links in
[protocol.md](protocol.md). Paths use `<user>`, `<version>` and `<hub package>` placeholders.

## JSON files

| File | Shape | Role |
|---|---|---|
| `projects-v1.json` | `{"schema_version":"v1","data":{"<path>":{...}}}` | *Legacy* project registry, no `tags`. Hub 4 imports it once and never writes it again; the CLI fallback is in [registry.md](registry.md#schema-and-behavior). See also [PRODUCT.md](../../../PRODUCT.md#scope-decisions) |
| `excludedProjectFolders.json` | `{"schema_version":"1","data":["<path>"]}` | Paths Hub has removed from the list. The extension does not write this file |
| `projectsInfo.json` | `{"<path>":{"cliArgs":"..."}}` | Extra editor arguments per project |
| `projectDir.json` | `{"directoryPath":"..."}` | Last folder used by Add / New project |
| `projectSortPreferences.json` | `{"sortBy":"lastModified","sortDirection":"ascending"}` | List sort |
| `projectTablePreferences.json` | `{"isFavorite":bool,"isVersionControlConnected":bool,"cloudServices":bool,"lastModified":bool,"version":bool,"tags":bool,"buildTarget":bool,"currentBranch":bool,"sizeInBytes":bool,"hideMissingProjects":bool}` | Visible columns and "hide missing projects" |
| `projectOrganizationSetting.json` | | Org filter |
| `hubInfo.json` | `{"version":"<version>","executablePath":"<hub package>\app\Unity Hub.exe"}` | Installed Hub, rewritten on each launch |
| `user-settings.json` | object | General settings (`minimizeToTray`, `themeSource`, `projects.showProductNames`, `deeplink.*`, `updater.releaseChannel`, `templates.*`, CLI consent flags). `unity config path` points here. `deeplink.alwaysForwardToSingleEditor = true` was observed here on one machine; see [protocol.md](protocol.md) |
| `hub-gpui-window-state.json` | `{x,y,width,height,maximized,fullscreen}` | Hub 4 window. `main-window.json` is *legacy* |
| `cachedFeatureFlags.json`, `cloudConfig.json`, `hubConfig.json` | | Remote flags and endpoints |
| `cli-plugin-registry.json`, `cli-skill-installs.json`, `cli-update-check.json` | | Unity CLI state |
| one-flag files | `hideHubOnEditorOpen`, `skipRemoveConfirmation`, `themeSetting`, `languageConfig`, `dismissedBanners`, ... | One preference each |

Editor-install and download preferences are in [install-locations.md](install-locations.md).

## Folders

| Folder | Content |
|---|---|
| `hub-gpui\` | Hub 4 caches: `downloadable-editors-cache.json` (releases by stream with download URLs), `live-platform-releases-cache.json`, `updates\` (staged Hub update MSIX) |
| `logs\` | JSON lines `{"level","time","pid","moduleName","msg"}`: `info-log.json` (+ rotations), `install-log.json`, `cli-log.json`, `broker-audit.json`, `hub-gpui-running-<pid>-<id>.json` while Hub runs |
| `downloads\` | Installers: `UnitySetup64-<version>.exe`, `UnitySetup-<Module>-Support-for-Editor-<version>.exe` |
| `Templates\` | Custom templates |

`DIPS`, `SharedStorage`, `IndexedDB`, `Local Storage` and `GPUCache` are Chromium leftovers from
Hub 3. The Hub 3 Electron folders under this path are not Hub 4 data.

## Running Hub

Hub is single-instance; the process is `Unity Hub.exe`, and `hubInfo.json` `executablePath` gives its
full path. While it runs, SQLite uses WAL mode, so `hub.db-wal` and `hub.db-shm` exist and
`logs\hub-gpui-running-<pid>-<id>.json` is written. `hubInfo.json` is rewritten on each launch.

The extension treats `hub.db-wal` existence as the running signal: without it the db file is
complete and read with `immutable=1` (no side files); with it, `readonly_shm=1` maps the existing
`-shm` read-only (`HubDatabase.ReadOnlyUri`). A normal start logs
`deeplink: argv carries no unityhub:// link`; protocol-driven starts are described in
[protocol.md](protocol.md#protocol-dispatch-and-running-hub).
