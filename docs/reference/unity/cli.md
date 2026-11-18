# Unity CLI (`unity`)

Separate product from Hub, own MSIX (winget `Unity.CLI`), on PATH as an app execution alias
(`%LOCALAPPDATA%\Microsoft\WindowsApps\unity`). Observed on 1.0.0; 1.0.1 notes from its changelog.
Manual: https://docs.unity.com/unity-cli (not https://docs.unity.com/en-us/hub/hub-cli, which is the Hub 3 `--headless` CLI). Changelog:
`https://public-cdn.cloud.unity3d.com/hub/prod/cli/<version>/CHANGELOG.md`.

## Output contract

- Global `--format human|json|tsv|ndjson|github` (env `UNITY_FORMAT`), `--json` shorthand. `tsv` is
  the default when stdout is piped; 1.0.1 adds a header row (`--no-header` drops it).
- JSON envelope for every command:

  ```json
  {"success": true, "command": "projects list",
   "data": {"items": [...], "total": 30, "nextPage": null, "partial": false},
   "errors": [{"code": "STATUS_NO_INSTANCES", "message": "..."}], "warnings": []}
  ```

  Listings page (`--limit`, `--all`). `success:false` comes with a stable `errors[].code`.
- Noise control: `--no-banner` (`UNITY_NO_BANNER`), `--quiet`, `--non-interactive`, `--no-pager`,
  `--no-color`. 1.0.1 moves intro/tip lines to stderr.
- `unity commands --format tsv` lists every command path with a description (1.0.1 adds a `path` key in JSON).

## Data it reads

- Config: `unity config path` -> `%APPDATA%\UnityHub\user-settings.json` (shared with Hub).
- **Projects: `projects-v1.json` minus `excludedProjectFolders.json`** (1.0.0). Evidence: the output
  has Hub 3 `sourceFingerprint` suffixes (`spn:false;eds:`, never `hub-gpui-derive`) and no `tags`,
  while the same rows in `hub.db` have both. Path sets alone do not tell the stores apart.
- `projects info` reads the project folder too: adds `scriptingBackend` and `packages` (resolved
  `Packages/manifest.json`), and `openInEditorUrl` appears in list items.
- Editors: `unity editors` mixes installed editors (with `location`, `modules`, `upgradeTo`) and
  suggested releases (version only). `-i` lists installed only, `-r` releases only. `-i` matches the
  folders that contain `<version>\Editor\Unity.exe`; leftover version folders without it are skipped.
- Running editors: `unity status` / `unity editors running` list editors with the Pipeline package
  (port, project, version, PID, state). Without it: `STATUS_NO_INSTANCES`.

## Commands relevant to a project launcher

| Command | Does |
|---|---|
| `unity projects [pattern] --json --all` | List registered projects (glob on title/path) |
| `unity projects info [path\|name] --json` | One project, with packages |
| `unity open [project]` | Open with the matching editor. `--editor-version`, `-e <path>`, `--build-target`, `--args`, `--accept-upgrades`, `--batchmode`, `--until-ready`, `--wait` |
| `unity projects open <name\|path>` | Open by name, fuzzy title or path |
| `unity close <project>` | Close the editor holding it (no save) |
| `unity projects add` / `remove` | Register / unregister folders |
| `unity projects pin` / `unpin` | Favorite by name or pattern |
| `unity projects require` | Install the needed editor if missing |
| `unity projects report` | Group projects by editor, flag missing installs and non-LTS |
| `unity projects size`, `clean`, `verify`, `upgrade` | Disk use, delete `Library`/`Temp`/..., integrity check, editor upgrade |
| `unity projects export` / `import` | Project list to/from JSON |
| `unity editors -i --json`, `editors path <ver>`, `editors default` | Installed editors |
| `unity install <ver>`, `install-modules`, `editors upgrade`, `editors prune` | Editor management |
| `unity editors install-path` / `install-path` | Show or set the install folder |
| `unity releases` | Available releases |
| `unity status`, `editors running` | Live editors |

Other groups (not launcher-relevant): `build`, `test`, `run`, `watch`, `ci`, `command`/`job`/`mcp`/
`pipeline` (drive a running editor), `auth`, `cloud`, `context`, `vcs`, `collaboration`, `templates`,
`assets`, `cache`, `config`, `hub install`, `diagnose`, `docs`, `shell`, `skill`, `upgrade`.

## Unverified

- Which store `projects pin`/`add`/`remove` write in 1.0.x (JSON or `hub.db`). Not tested to avoid
  changing real data.
- Whether 1.0.1 reads `hub.db`; its changelog says nothing about it.
