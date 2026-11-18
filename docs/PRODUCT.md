# Product

Single source for what the extension does, how it looks, and what comes next. The README only
links here. Technical background: [reference/unity/](reference/unity/README.md).

## Purpose

Open any Unity project from Command Palette in one keystroke, without starting Unity Hub. The
extension reads Hub's local data and starts the Unity Editor itself.

Not affiliated with Unity Technologies. Local data only: no network, no Unity account.

## Scope decisions

| Decision | Choice | Why |
|---|---|---|
| Hub versions | **Hub 4 first, Hub 3 still supported** | Hub 4 is current, but the JSON reader already exists and costs little to keep |
| Project source | `hub.db` if it exists and `meta.projects_json_migrated = '1'`, else `projects-v1.json` minus `excludedProjectFolders.json` | Hub 4 stops updating the JSON after it imports it |
| Hub 4 -> Hub 3 downgrade | Use whichever of `hub.db` / `projects-v1.json` was modified more recently | Otherwise Hub 3 edits would be hidden behind a stale `hub.db` |
| Writes to Hub data | **Never** | Hub overwrites outside edits, and a bad write could damage the user's project list. Dropped: "overwrite timestamp in Hub JSON" |
| Unity CLI | Not a data source. Optional actions only, if `unity` is on PATH | CLI 1.0 reads the stale JSON, and starting it is slow |
| Editor launch | `Unity.exe -projectPath "<path>"`, started directly | Hub does not need to be running |
| Platform | Windows x64 and arm64, PowerToys Command Palette | |

## Current behavior

One top-level command, **Unity Projects**, opens the page **Recent Unity Projects**.

| Element | Behavior |
|---|---|
| Order | Newest `lastModified` first. Favorites first if the setting is on |
| Title | Hub's project title, or the folder name |
| Subtitle | Project path. Adds "(folder not found)" when missing |
| Tags | Star + "Favorite" (gold), "Missing" (red), editor version |
| Icon | Unity logo, or a "missing" icon |
| Enter | Opens the project in its matching editor. Toasts: "Project folder no longer exists", "Unity <version> not found. Please install through Unity Hub." |
| More commands | Open in File Explorer |
| Empty list | "No Unity projects found" with a hint to open projects in Hub |
| Data source | `hub.db` (Hub 4), else `projects-v1.json` minus `excludedProjectFolders.json`; see scope decisions. Unreadable `hub.db` -> empty list |

Settings:

| Setting | Default |
|---|---|
| Group favorites first | on |
| Hide projects missing from disk | off |

Editors are found through the registry (`HKCU`/`HKLM\SOFTWARE\Unity Technologies\Installer`) and
matched by exact version.

## Roadmap

Ordered by priority.

| # | Item | Notes |
|---|---|---|
| 2 | Refresh when Hub data changes | Watch `hub.db`, `hub.db-wal` |
| 3 | Show and search tags | `tag:` search, Hub 4 only |
| 4 | Visual polish | Show `~` for the home folder; star without the word "Favorite" |
| 5 | Editor not installed: offer install or another editor | Today: toast only |
| 6 | Open with another installed editor | Needs an editor scan |
| 7 | List installed editors and their paths | Sub-page |
| 8 | Render pipeline and build target as tags | `renderPipeline`, `buildTarget` |
| 9 | Per-project editor arguments | `projectsInfo.json` `cliArgs` |
| 10 | "Installing <version>" status | `install-state.db` |
| 11 | Projects Hub doesn't list | Editor's recent-projects registry list ([community.md](reference/unity/community.md)) |
| 12 | Git info: branch, open the repo online | `vcs*` fields |
| 13 | CLI actions when `unity` is on PATH: clean, size, upgrade, close | Optional |

Done: settings to sort and filter the list; read `hub.db` (Hub 4) with JSON fallback and the downgrade rule.

Dropped: writing to Hub's JSON or databases.
