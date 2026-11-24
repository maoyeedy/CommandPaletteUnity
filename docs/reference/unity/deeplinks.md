# Unity Hub deeplinks (Windows)

Facts sourced to docs.unity.com or unity.com are official. Facts marked "observed" come from this
machine's registry, `hubInfo.json` and `logs\info-log.json` (Hub 4.0.1, checked 2026-11-24). The
extension never runs the Unity CLI; this page is reference only.

## Editor install: `unityhub://<version>/<changeset>`

- The Releases API exposes `unityHubDeepLink`, e.g. `unityhub://2020.3.44f1/7f159b6136da`.
- Hub 4.0.0-beta.2 release notes: a `unityhub://` link installs a custom or pre-release build through
  the normal install flow, lists it beside an official release of the same version, and only accepts
  builds hosted on Unity's own download sites.
- Observed: `unityhub://6000.3.5f2/3fa8bc678cb0` was logged by `ArgumentParser`, followed by
  "Editor version 6000.3.5f2 is not installed" and "Loading custom install modal".
- Observed module form: `unityhub://6000.3.5f1/a1ec4b2f2d19/module=mac-mono` led to a custom install
  of `mac-mono`. No official page documents this form, so treat it as observed only.
- The link needs a short changeset, not just a version. A `hub.db` project row has a `changeset`
  field, but it can be empty (see [hub-data.md](hub-data.md)).

## Project and editor operations: `unityhub://editor/...`

- Hub 3.15+ with local projects made in Editor 6.3+ supports deeplinks for new projects, package
  install, Asset Store download, custom handlers and cloud projects.
- The `https://link.unity.com/hub/...` redirect form is meant for links that need a browser fallback;
  an HTML redirect to a native app always requires user confirmation. Native apps and local
  processes should use `unityhub://editor/...` directly to avoid that prompt.
- Official Windows example: `start "" unityhub://editor/project/new`; from C#,
  `Application.OpenURL("unityhub://editor/project/new")`.

## Hub does not need to be open

The official deeplinking page states: "The user doesn't need to have the Hub open prior to accessing
the deeplink." The OS protocol dispatch starts it; no running-Hub check is needed before invoking a
link. Observed details on this machine:

- `HKCR\unityhub` and `HKCU\Software\Classes\unityhub` carry only the `URL Protocol` marker. The MSIX
  package registers `windows.protocol\unityhub` under AppModel with `ACID` = `app\Unity Hub.exe`.
  There is no classic `shell\open\command` here. `hubInfo.json` holds `executablePath` for the
  resolved path.
- Invoking a URL while Hub runs logs a second-instance event with `protocolUrlSource: 'sharedData'`
  and "protocol URL forwarded to protocol handler". Normal starts log
  "deeplink: argv carries no unityhub:// link".
- `user-settings.json` carries `deeplink.alwaysForwardToSingleEditor = true` here.
- If a running check is ever needed for another purpose, see [hub-data.md#running-hub](hub-data.md#running-hub):
  process `Unity Hub.exe`, `hubInfo.json`, and `hub.db-wal` / `logs\hub-gpui-running-*.json`.

## Unknowns - do not assume

- No official page enumerates all `unityhub://` routes, nor documents a module-install-only URL.
- No documented URL opens a project by local path. A June 2025 community request asks for
  `unityhub://open?projectPath=` as a new feature; a third-party wiki's `unityhub://open?path=...`
  is unverified and states Unity has no built-in local file-open deeplink.
- Hub's "Copy link" project action (3.21.0, for GitHub, GitLab and Unity Version Control projects
  connected to Unity Cloud) produces a shareable link, but its literal URL is not documented here.
- Whether the Hub 4 native rewrite preserves every Hub 3 route is undocumented; the editor-install
  link and the `unityhub://editor/*` family are confirmed for current versions.

## Sources

- https://docs.unity.com/en-us/hub/deeplinking-support (updated 2026-11-13)
- https://docs.unity.com/en-us/oas-app-linking/1.0.0 (updated 2026-08-11)
- https://docs.unity.com/en-us/oas-release/1.0.0 (`unityHubDeepLink` field)
- https://unity.com/unity-hub/release-notes (Hub 4.0.0 beta, 3.21.0, 3.22.0 notes)
- https://discussions.unity.com/t/hub-custom-protocol-handlers/1659289/1 (community, 2025-06-25)
