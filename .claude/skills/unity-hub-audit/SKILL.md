---
name: unity-hub-audit
description: >-
  Re-audit how Unity Hub and the Unity CLI store and expose project and editor data on Windows (hub.db, projects-v1.json, settings files, `unity` command output) and update docs/reference/unity to match. Use after a Hub or CLI upgrade, when the extension's project list looks wrong or stale, or when asked what Hub/CLI store, where, or whether the reference docs are still current.
---

# Unity Hub / CLI audit

Goal: find what changed in Hub and CLI behavior since the last audit, prove it, and update
`docs/reference/unity/` (README, hub-data, hub-ui, cli, extension-impact, community, rea) and
`references/baseline.json`. For database schemas, follow the recipe in `docs/reference/unity/rea.md`.

## 1. Snapshot and diff

Run with uv Python from the repo root (takes a minute; it calls `unity` several times):

```bash
T=$(cygpath -m "$(mktemp -d)")
uv run python -I <skill-dir>/scripts/snapshot.py "$T/snap.json" --diff <skill-dir>/references/baseline.json
```

The snapshot is structural only (schemas, field names, presence counts, file names, command paths,
which store the CLI reads). Lines: `+` new, `-` gone, `~` changed. Ignore drift that is just the
user's data (project/editor counts, presence counts moving together). Look for:

- `hubVersion` / `cliVersion`: confirm the upgrade actually happened (Hub restarts to apply its staged
  update; `winget show Unity.UnityHub` / `Unity.CLI` give the latest).
- `hubDb` schema, `hubDbMeta`, new `hubDbProjects.fields`, new top-level files in `dataFolder`.
- `cliProjects.fingerprintSuffixes` and `hubVsJson.cliRowsWithTags`: if the CLI shows
  `hub-gpui-derive` suffixes or tags, it moved to `hub.db`. That flips a key finding.
- `editors`: CLI vs folder-scan agreement.
- `cliCommands`: added/removed commands; read `unity <cmd> --help` for any launcher-relevant ones.

## 2. Check what the snapshot cannot

- Release notes: Hub post on discussions.unity.com, CLI `https://public-cdn.cloud.unity3d.com/hub/prod/cli/<ver>/CHANGELOG.md`.
  Search for registry, projects, tags, hub.db, editors. Ignore https://docs.unity.com/en-us/hub/hub-cli: it documents the Hub 3 `--headless` CLI, not the `unity` CLI.
- UI changes: the snapshot does not see the Hub window. Ask the user for screenshots of the
  Projects page, column picker, expanded row and any new dialogs, or drive Hub with the
  winapp-ui-automation skill.
- A new field: make a change in Hub (the user does it, e.g. tag a scratch project) and find it in a
  copy of `hub.db`. Never write to `hub.db`, `projects-v1.json` or run mutating CLI commands
  (`projects pin/add/remove`) on real data without asking: they change the user's Hub state.
- Items listed under "Unverified" in `cli.md`: retry them when the version changes.

## 3. Update the docs

- Edit facts in place; state the versions a fact was observed on when behavior differs by version
  (keep the old behavior if users may still run it). Update the version table in README.md.
- The docs are public: use `<user>`, `<project>`, `<version>` placeholders, no real paths, project,
  org, account or machine names, no OS or editor build details.
- Revisit `extension-impact.md` if the data source answer changed.
- Replace `references/baseline.json` with the new snapshot once the docs match it, and
  rg it for the user's name and drive-letter paths before saving.
