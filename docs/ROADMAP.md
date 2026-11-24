# Roadmap

Ordered by impact on finding and launching a project, not by how much Hub 4 data we can read. Scope
and current behavior live in [PRODUCT.md](PRODUCT.md); ideas we decided against are in
[Rejected](PRODUCT.md#rejected).

| # | Item | Notes |
|---|---|---|
| 1 | Projects Hub doesn't list | Editor's recent-projects registry list ([community.md](reference/unity/community.md)). Read-only fallback, deduped by path and labeled so it never looks like a Hub row |
| 2 | Missing editor: Hub install prompt | Today: toast only. `unityhub://<version>/<changeset>` opens Hub's install modal; Hub does not need to be running (see [deeplinks.md](reference/unity/deeplinks.md)) |
| 3 | Show and search tags | `tag:` search, Hub 4 only. `UnityProject.Tags` is already parsed; show tags in details first. Search syntax needs a dedicated UI/UX pass |
| 4 | Visual polish | Show `~` for the home folder; star without the word "Favorite" |
| 5 | Git info in details | `vcsProvider`, `organizationName`, `repositoryName` from `hub.db`. Branch is computed live, so it stays out |
| 6 | Render pipeline / build target in details | `renderPipeline`, `buildTarget`; details only, not row tags |
| 7 | Per-project editor arguments | `projectsInfo.json` `cliArgs`; relevant only where users already set them |

Done: refresh when Hub data changes (`hub.db`, `hub.db-wal`, JSON fallback); settings to sort and filter the list; read `hub.db` (Hub 4) with JSON fallback and the downgrade rule; remove from Hub's list (per project, all missing, or all entries, Hub 4 only).
