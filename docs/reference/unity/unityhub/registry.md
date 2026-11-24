# Unity Hub project registry (`hub.db`)

Hub 4 keeps its Projects list in `%APPDATA%\UnityHub\hub.db` (SQLite in WAL mode). Read it read-only,
or copy `.db`, `-wal` and `-shm` together. Files marked *legacy* came from Hub 3. JSON preferences,
logs and running indicators are in [config-and-logs.md](config-and-logs.md); install locations are in
[install-locations.md](install-locations.md).

## Schema and behavior

```sql
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
-- ('projects_json_migrated', '1') after the one-time import of projects-v1.json
CREATE TABLE projects (
  path       TEXT PRIMARY KEY,  -- project folder, Windows path
  data       TEXT NOT NULL,     -- JSON object, fields below
  updated_at INTEGER NOT NULL   -- Unix ms of the last row write
);
```

- One row per project in the Projects list. UI edits (favorite, tags, editor version, platform)
  write the row within seconds.
- On first Hub 4 launch, `projects-v1.json` is imported, `meta.projects_json_migrated` is set to
  `'1'`, and the JSON is never written again. The CLI 1.0.x still reads the JSON as a Hub 3 fallback
  (observed on 1.0.0; see [../cli.md](../cli.md)). The extension's fallback/downgrade rule is in
  [PRODUCT.md](../../../PRODUCT.md#scope-decisions).
- **Remove from list** deletes the `projects` row and appends the path to
  `excludedProjectFolders.json`. The CLI `projects remove` and this extension delete only the row;
  files on disk are not deleted.
- Hub re-reads project folders on demand and rewrites changed rows. Log line:
  `project refresh from disk: <n> row(s) rewritten`.

## Project record fields

"Usually" = present for projects opened at least once in a recent editor.

| Field | Presence | Type | Meaning |
|---|---|---|---|
| `title` | always | string | Display name. Folder name unless renamed |
| `hasCustomDisplayName` | usually | bool | `title` was renamed in Hub |
| `path` | always | string | Same as the row key |
| `containingFolderPath` | usually | string | Parent folder |
| `version` | always | string | Editor version, e.g. `6000.0.1f1` |
| `changeset` | always | string | Editor revision hash, may be `""`; needed by the install link in [protocol.md](protocol.md) |
| `architecture` | always | string | `x86_64` or `arm64` |
| `isCustomEditor` | always | bool | Editor set to a path outside Hub's installs |
| `isFavorite` | always | bool | Star |
| `lastModified` | always | Unix ms or **`null`** | `null` if never opened; absent values are treated as `0` by `ProjectParser` |
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
| `tags` | if any | string[] | **Hub 4 only**, up to 50 chars each. UI: [../hub-ui.md#manage-tags-dialog](../hub-ui.md#manage-tags-dialog) |

Computed live, not stored: current branch, folder size, remote repo size, Git account, and warnings
such as "Missing Editor version" (presentation: [../hub-ui.md](../hub-ui.md), install route:
[protocol.md](protocol.md), CLI report: [../cli.md](../cli.md)).

## Other databases

| File | Tables | Use |
|---|---|---|
| `install-state\install-state.db` | `downloads` (uid, version, arch, modules_json, state `queued/downloading/complete/failed/cancelled`, bytes_total, bytes_done, pid, timestamps, error), `installs` (at most one row, phase `queued/installing`), `file_claims`, `schema_meta`. `downloads` and `installs` have `writer_kind` (`hub`, `hub-native`, `cli`): which process owns the row | Editor download/install queue, shared across processes. Empty when idle |
| `accounts.db` | `accounts`, `active_account`, `meta` | Signed-in Unity accounts. Personal data; tokens live in the OS credential store. Never read it |

Schema refreshes use the recipe in [../rea.md](../rea.md). Do not put row content in the docs:
`projects.data` holds paths and organization names.
