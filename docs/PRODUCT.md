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
| Writes to Hub data | **Deletes `projects` rows only:** "Remove from list" deletes one row; the danger zone deletes all rows, both in `hub.db` through `HubDatabase`. Nothing else under `%APPDATA%\UnityHub` | Same kind of write as `unity projects remove` (verified, see [cli.md](reference/unity/cli.md)). Any wider write risks the user's project list |
| Unity CLI | **Reference only.** The extension never runs, requires or bundles it | Most users have only Hub. We study its commands, UX and data writes, then implement what we need ourselves |
| Hub 3 users | Read-only: list, open, Explorer. No remove | Hub 3 is end-of-life (3.22.6); its list lives in JSON we don't write |
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
| More commands | Open in File Explorer. Hub 4 only: "Remove from Unity Hub list" (confirm, deletes the `hub.db` row), and on missing projects "Remove all missing projects (n)" |
| Empty list | "No Unity projects found" with a hint to open projects in Hub |
| Settings page | Top-level "Settings" page with the two toggles and a **Danger zone**: a destructive "Remove all projects from Unity Hub list" button, one critical confirm, then all `hub.db` project rows are deleted. No project folder is deleted. Hub 4 only |
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
| 1 | Refresh when Hub data changes | Watch `hub.db`, `hub.db-wal` |
| 2 | Show and search tags | `tag:` search, Hub 4 only |
| 3 | Visual polish | Show `~` for the home folder; star without the word "Favorite" |
| 4 | Editor not installed: offer install or another editor | Today: toast only |
| 5 | Open with another installed editor | Needs an editor scan |
| 6 | List installed editors and their paths | Sub-page |
| 7 | Render pipeline and build target as tags | `renderPipeline`, `buildTarget` |
| 8 | Per-project editor arguments | `projectsInfo.json` `cliArgs` |
| 9 | "Installing <version>" status | `install-state.db` |
| 10 | Projects Hub doesn't list | Editor's recent-projects registry list ([community.md](reference/unity/community.md)) |
| 11 | Git info: branch, open the repo online | `vcs*` fields |
| 12 | Project tools: clean, size | Own code, modeled on `unity projects clean` / `size` |

Done: settings to sort and filter the list; read `hub.db` (Hub 4) with JSON fallback and the downgrade rule; remove from Hub's list (per project, all missing, or all entries, Hub 4 only).

## Rejected

Decided against; don't propose again without new facts.

| Idea | Why not |
|---|---|
| Extension-local favorites or hide list | Hub is the single source of truth; a second list drifts from it and confuses users |
| Running the Unity CLI (PATH or Hub 4's copy) | Not installed for most users, slow to start, output can change between versions. It stays a reference |
| Bundling the Unity CLI | Proprietary, and redundant with the reference-only rule |
| Writing Hub 3 JSON (`projects-v1.json`, `excludedProjectFolders.json`) | Hub 3 is end-of-life; Hub 4 ignores the JSON after migration |
| Overwriting the timestamp in Hub data | Hub owns it; no user benefit worth a write |
| Any `hub.db` write other than deleting a `projects` row | Favorites, tags, titles are edited in Hub |
