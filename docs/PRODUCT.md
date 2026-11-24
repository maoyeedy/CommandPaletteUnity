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
| Order | Ordered by `Sort projects by` (default newest `lastModified` first; also supports `Name (A-Z)` and `Editor version`). Favorites first if enabled; optionally grouped under section headers by editor version |
| Title | Hub's project title, or the folder name |
| Subtitle | Project path (with `~` for `%USERPROFILE%`). Adds "(folder not found)" when missing |
| Tags | Star icon (gold, tooltip "Favorite"), "Missing" (red), editor version |
| Icon | Unity logo, or a "missing" icon |
| Details | Details pane (toggled by `Show details pane`) showing relative `Modified` time at top, editor version, full path, Hub 4 tags, Git repository (clickable link for GitHub/GitLab), render pipeline, and build target (omitting empty fields) |
| Enter | Opens the project in its matching editor (re-scanning the registry on miss). Missing editor with a known changeset prompts to install via `unityhub://<version>/<changeset>`; otherwise toasts "Unity <version> not found. Please install through Unity Hub.". Missing folder toasts "Project folder no longer exists" |
| More commands | Kept `<= 5` items and grouped by verb (`Open ...` -> `Copy ...` -> critical `Remove ...` at the bottom): when folder exists, `Open in Console` (`Ctrl+Shift+R`, secondary action), `Open in Explorer` (`Ctrl+Shift+E`), and `Open solution` (when a `.sln`/`.slnx` exists in the project root); always `Copy path` (`Ctrl+Shift+C`); Hub 4 only `Remove from Hub list` (`Ctrl+Shift+Delete`, confirm, deletes the `hub.db` row), and on missing projects with >1 missing `Remove all missing (n)` |
| Empty list | "No Unity projects found" with a hint to open projects in Hub |
| Settings page | The extension's real settings page (provider `Settings`), with the settings below and a **Danger zone**: a "Remove all projects from Hub list" button, an inline two-step confirm inside the settings card (CmdPal's `CommandResult.Confirm` dialog belongs to the palette window, not the settings window), then all `hub.db` project rows are deleted. No project folder is deleted. Hub 4 only |
| Data source | `hub.db` (Hub 4), else `projects-v1.json` minus `excludedProjectFolders.json`; see scope decisions. Unreadable `hub.db` -> empty list. Watches Hub data files (`hub.db`, `hub.db-wal`, `projects-v1.json`, `excludedProjectFolders.json`) and refreshes the open list automatically on change |

Settings:

| Setting | Default |
|---|---|
| Sort projects by | Recently modified |
| Group favorites first | on |
| Group by editor version | off |
| Hide missing projects | off |
| Show details pane | on |

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
| Projects Hub doesn't list (Editor `RecentlyUsedProjectPaths` registry fallback) | Rejected as overkill: requires reading `ProjectVersion.txt` from disk, recency order is unverified, and adds a second non-Hub project type without tags, favorites, or remove |
| Custom `tag:` search syntax | Rejected as overkill: converting `ListPage` to `DynamicListPage` replaces Command Palette's built-in fuzzy search for minimal gain; tags are shown in the Details pane instead |
| Per-project editor arguments (`projectsInfo.json` `cliArgs`) | Rejected as overkill: rarely set; Unity Hub remains the place for custom launch flags |
| Details pane `HeroImage` (Unity logo) | Redundant with the list row icon and wastes vertical space above the project title |
| Rendering details pane metadata in `Details.Body` Markdown | Command Palette's host `DetailsPane.xaml` keeps fixed margins on empty `Body` (`0,4,0,24`) and `Content` (`0,4,0,16`) rows that extensions cannot collapse; replacing `Metadata` with Markdown loses native `DetailsTags`/`DetailsLink` styling and hits light-theme contrast issues. Keep native `Title` + `Metadata` without `HeroImage` |
| Extra More commands (`Open on GitHub/GitLab`, `Open Unity Hub`) | Keeps the More menu `<= 5` items grouped by verb (`Open` -> `Copy` -> red `Remove` at the bottom); GitHub/GitLab is already a clickable link in the Details pane |
