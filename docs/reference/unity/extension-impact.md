# Impact on the extension

## The data source is stale on Hub 4

`UnityExtension/Helpers/Resources.cs` reads `%APPDATA%\UnityHub\projects-v1.json`. Hub 4 stops writing
it after migration, so new projects, removals, favorites, version changes and tags never appear.

Proposed source order:

1. `hub.db`, when it exists and `meta.projects_json_migrated = '1'`: `SELECT path, data FROM projects`,
   read-only. Parse `data` with the existing field names.
2. Else `projects-v1.json` (Hub 3), minus paths in `excludedProjectFolders.json`.
3. Downgrade guard: if `projects-v1.json` is newer than `hub.db`, use the JSON.

Scope decisions live in [PRODUCT.md](../../PRODUCT.md#scope-decisions).

Do not shell out to the CLI for the list: 1.0.0 reads the stale JSON, and process startup is slow.

Open points:

- Trim/AOT-safe SQLite: `Microsoft.Data.Sqlite` with a SQLitePCLRaw bundle, or P/Invoke to Windows'
  `winsqlite3.dll`. Versions go in `Directory.Packages.props`.
- Refresh: watch `hub.db` and `hub.db-wal`, not the JSON.
- `lastModified` can be `null`; `tags` may be missing. Keep `ProjectParser`'s swallow-and-empty rule.
- Tests: a db `data` row with `tags` and `null` `lastModified`.
- Read only. Writing `hub.db` while Hub runs can be overwritten by Hub's in-memory state.

## Safety rules

- Never write any file in `%APPDATA%\UnityHub`. Open `hub.db` read-only and do not take write locks.
- Never read `accounts.db`. Never log or show raw project-record JSON; show paths and titles only.
- Launch `Unity.exe` directly with `-projectPath "<path>"`, after checking that the folder exists.
- Match editor versions exactly. See [community.md](community.md) for more pitfalls.
- Audit Hub with static tools only ([rea.md](rea.md)); never run Hub or the CLI under a debugger.

## Feature ideas

Moved to the roadmap in [PRODUCT.md](../../PRODUCT.md#roadmap).
