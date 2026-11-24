# Roadmap

Ordered by priority. Scope and current behavior live in [PRODUCT.md](PRODUCT.md); ideas we decided
against are in [Rejected](PRODUCT.md#rejected).

| # | Item | Notes |
|---|---|---|
| 1 | Show and search tags | `tag:` search, Hub 4 only |
| 2 | Visual polish | Show `~` for the home folder; star without the word "Favorite" |
| 3 | Editor not installed: offer install or another editor | Today: toast only |
| 4 | Open with another installed editor | Needs an editor scan |
| 5 | List installed editors and their paths | Sub-page |
| 6 | Render pipeline and build target as tags | `renderPipeline`, `buildTarget` |
| 7 | Per-project editor arguments | `projectsInfo.json` `cliArgs` |
| 8 | "Installing <version>" status | `install-state.db` |
| 9 | Projects Hub doesn't list | Editor's recent-projects registry list ([community.md](reference/unity/community.md)) |
| 10 | Git info: branch, open the repo online | `vcs*` fields |
| 11 | Project tools: clean, size | Own code, modeled on `unity projects clean` / `size` |

Done: refresh when Hub data changes (`hub.db`, `hub.db-wal`, JSON fallback); settings to sort and filter the list; read `hub.db` (Hub 4) with JSON fallback and the downgrade rule; remove from Hub's list (per project, all missing, or all entries, Hub 4 only).
