# Unity Hub 4 Projects page and tags

Quoted UI text uses `-` where Hub shows a middle dot.

## Layout

- Title bar: Hub logo, sidebar toggle; right side: Learn, inbox, notifications, Services, Settings
  (dot = update ready), account avatar.
- Sidebar: **Projects**, **Templates**, **Installs**.
- Header: **Search**, **Add** dropdown (from disk, from repository), **+ New project**.
- Filter chips: **Editor version**, **Platform**. Tags also filter the list.

## Table

Default columns: expand chevron, star, **Name** (title, path below), **Platform**, **Editor version**,
**Modified** (relative time, sortable), column-picker gear, row `...` menu.

The gear adds: version control (Git provider icon), Unity Cloud (cloud icon), **Tags**,
**Current branch**, **Size**, and a "hide missing projects" toggle. Saved in `projectTablePreferences.json`.

Cells:

- **Platform**: build target name (`Windows 64-bit`), `Current platform` when unset, dash when
  unknown. Up/down arrows open a picker.
- **Editor version**: version string with a picker of installed editors.
- **Warning icon** before the name when the project cannot open. Tooltip:
  > This project can't be opened in the Editor. To proceed, resolve the following issues:
  > - Missing Editor version
  The install flow can also be opened with a deeplink (see [unityhub/protocol.md](unityhub/protocol.md)).
- **Tags**: chips. **Branch**: live Git branch, dash without Git.

## Expanded row

The row header shows icons for star, Git provider (repo linked) and cloud (Unity Cloud linked).

**Configuration**

| Label | Shows | Source |
|---|---|---|
| Editor version | version, picker, stream badge (`Alpha`, `Beta`, `LTS`), `Release notes` link | `version`, release cache |
| Modified | relative and absolute time | `lastModified` |
| Render Pipeline | `Universal Render Pipeline (URP)` | `renderPipeline` |
| Size | `298.8 MB` | live scan |
| Custom templates | count | templates made from this project |

**Connections**

| Label | Shows | Source |
|---|---|---|
| Unity organization | org link | `organizationId` |
| Unity Cloud project | project link, `Disconnect from Unity Cloud` | `projectName`, `cloudProjectId` |
| Source control repository | repo link, `Disconnect from GitHub` | `vcsProvider`, `organizationName`, `repositoryName` |
| Current branch | branch | live Git |
| Git account | user, `Credential manager default` badge, `Change` | OS credential manager, per-project override |
| Remote repository size | `23.85 MB` | provider API |

## Manage tags dialog

From the row `...` menu. "Organize this project with one or more tags. Filter your projects by tag
from the Projects list." Input hint: "Press Enter or , to add - 50 characters max". Collapsible
**Tag suggestions**: game, prototype, simulation, visualization, 2d, 3d, client project, personal,
archived, work in progress. Cancel / Save. Saved to the `tags` array of the `hub.db` row (see
[unityhub/registry.md#project-record-fields](unityhub/registry.md#project-record-fields)).
