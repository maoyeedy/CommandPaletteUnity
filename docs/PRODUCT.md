# Product

Single source for what the extension does and how it looks. What comes next is in
[ROADMAP.md](ROADMAP.md). Technical background: [reference/unity/](reference/unity/README.md).

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
| Settings page | The extension's real settings page (provider `Settings`), with the two toggles and a **Danger zone**: a "Remove all projects from Unity Hub list" button, an inline two-step confirm inside the settings card (CmdPal's `CommandResult.Confirm` dialog belongs to the palette window, not the settings window), then all `hub.db` project rows are deleted. No project folder is deleted. Hub 4 only |
| Data source | `hub.db` (Hub 4), else `projects-v1.json` minus `excludedProjectFolders.json`; see scope decisions. Unreadable `hub.db` -> empty list. Watches Hub data files (`hub.db`, `hub.db-wal`, `projects-v1.json`, `excludedProjectFolders.json`) and refreshes the open list automatically on change |

Settings:

| Setting | Default |
|---|---|
| Group favorites first | on |
| Hide projects missing from disk | off |

Editors are found through the registry (`HKCU`/`HKLM\SOFTWARE\Unity Technologies\Installer`) and
matched by exact version.

## Roadmap

Tracked in [ROADMAP.md](ROADMAP.md), ordered by priority.

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
| Open a project with a different installed editor | Unity projects are version-bound; a mismatched version can upgrade or break the project. Hub's Installs page stays the place to choose editors |
| List installed editors and their paths inside CmdPal | Duplicates Hub's Installs page and offers no launch action |
| "Installing <version>" status from `install-state.db` | Transient, usually empty second database; the launcher cannot act on the status, so watching it costs more than the rare state is worth |
| Project tools: clean, size | `clean` deletes Library/Temp (destructive); `size` needs a slow live scan. Both belong to Hub/the CLI, which stays reference-only |
