# Roadmap

No open roadmap items remain for 1.0. Scope and current behavior live in [PRODUCT.md](PRODUCT.md);
dropped ideas are recorded in [Rejected](PRODUCT.md#rejected).

## Shipped in 1.0

- Refresh automatically when Hub data changes (`hub.db`, `hub.db-wal`, JSON fallback).
- Settings to sort favorites first, hide missing projects, and clear Hub's project list.
- Read `hub.db` (Hub 4) with `projects-v1.json` fallback and the Hub 4 -> Hub 3 downgrade rule.
- Remove from Hub's list (per project, all missing, or all entries; Hub 4 only).
- Visual polish: `~` for `%USERPROFILE%` in subtitles and a star-only favorite badge.
- Missing editor prompt: launch `unityhub://<version>/<changeset>` when a changeset is present, and re-scan the registry on miss.
- Project details pane: editor version, full path, Hub 4 tags, Git repository link/name, render pipeline, and build target.

## Rejected as overkill

Removed from the 1.0 roadmap (see [Rejected](PRODUCT.md#rejected) for rationale):

- Projects Hub doesn't list (Editor `RecentlyUsedProjectPaths` registry fallback).
- Custom `tag:` search syntax.
- Per-project editor arguments (`projectsInfo.json` `cliArgs`).
