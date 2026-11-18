# Community launchers and mods

Lessons from third-party Hub projects. They predate Hub 4, so none of them reads `hub.db`. Reviewed
2026-11-18, read-only.

| Project | Stack | Takeaway |
|---|---|---|
| [Leinnan/rusty_hub](https://github.com/Leinnan/rusty_hub) | Rust, egui | Reads `projects-v1.json` (`data` map, `path` field with the key as fallback) and never writes it. Also merges the Editor's recent-projects registry list |
| [Ravbug/UnityHubNative](https://github.com/Ravbug/UnityHubNative) | C++, wxWidgets | Keeps its own project list and never reads Hub's files. Useful mainly for its bugs |
| [unitycoder/UnityHubModding](https://github.com/unitycoder/UnityHubModding), [UnityHubPatcher](https://github.com/unitycoder/UnityHubPatcher) | Hub 3 `app.asar` and CSS edits | Obsolete for Hub 4. No data formats |

License-patching tools for Hub and the Editor are out of scope.

## Editor launch, as both launchers do it

- `<editor>\Editor\Unity.exe -projectPath "<project>"`, started directly, not through a shell.
- Optional `-buildTarget <target>`, then the user's extra arguments.
- New project: `-createProject <path> -cloneFromTemplate <template.tgz>`.
- Templates ship in `<editor>\Editor\Data\Resources\PackageManager\ProjectTemplates\*.tgz`.

## Extra data source

The Editor (not Hub) writes recent projects to HKCU
`Software\Unity Technologies\Unity Editor 5.x`, values whose name contains
`RecentlyUsedProjectPaths-`, as NUL-terminated UTF-8 binary. This covers projects missing from Hub.

## Pitfalls seen in their code

| Pitfall | Do instead |
|---|---|
| Matching an editor by substring: `2022.3.1` matches `2022.3.10f1` | Match the full version exactly (`EditorParser` already does) |
| Reading `ProjectVersion.txt` by cutting a fixed 17 characters | Check for the `m_EditorVersion:` prefix and take the next token |
| Scanning an editor folder that does not exist throws | Treat a missing folder as empty |
| Converting paths through the ANSI code page | Keep paths as UTF-16 strings |
| Logging or showing `projects-v1.json` content | rusty_hub notes it may hold account data. Show paths only |
