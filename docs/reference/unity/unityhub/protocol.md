# Unity Hub protocol (`unityhub://`)

Facts sourced to docs.unity.com or unity.com are official. Facts marked "observed" come from this
machine's registry, `hubInfo.json` and `logs\info-log.json` (Hub 4.0.1, checked 2026-11-24). The
extension never runs the Unity CLI; this page is reference only. Running-Hub filesystem indicators
live in [config-and-logs.md](config-and-logs.md#running-hub).

## Editor install: `unityhub://<version>/<changeset>`

- The Releases API exposes `unityHubDeepLink`, e.g. `unityhub://2020.3.44f1/7f159b6136da`.
- Hub 4.0.0-beta.2 release notes: a `unityhub://` link installs a custom or pre-release build through
  the normal install flow, lists it beside an official release of the same version, and only accepts
  builds hosted on Unity's own download sites.
- Observed: `unityhub://<version>/<changeset>` was logged by `ArgumentParser`, followed by
  "Editor version <version> is not installed" and "Loading custom install modal".
- Observed module form: `unityhub://<version>/<changeset>/module=<module>` led to a custom install of
  that module. No official page documents this form, so treat it as observed only.
- The link needs a short changeset, not just a version. A `hub.db` project row has a `changeset`
  field, but it can be empty (see [registry.md](registry.md#project-record-fields)).

## Project and editor operations: `unityhub://editor/...`

- Hub 3.15+ with local projects made in Editor 6.3+ supports deeplinks for new projects, package
  install, Asset Store download, custom handlers and cloud projects.
- The `https://link.unity.com/hub/...` redirect form is meant for links that need a browser fallback;
  an HTML redirect to a native app always requires user confirmation. Native apps and local
  processes should use `unityhub://editor/...` directly to avoid that prompt.
- Official Windows example: `start "" unityhub://editor/project/new`; from C#,
  `Application.OpenURL("unityhub://editor/project/new")`.

## Protocol dispatch and running Hub

The official deeplinking page states: "The user doesn't need to have the Hub open prior to accessing
the deeplink." The OS protocol dispatch starts it; no running-Hub check is needed before invoking a
link. Observed details on this machine:

- `HKCR\unityhub` and `HKCU\Software\Classes\unityhub` carry only the `URL Protocol` marker. The MSIX
  package registers `windows.protocol\unityhub` under AppModel with `ACID` = `app\Unity Hub.exe`.
  There is no classic `shell\open\command` here. `hubInfo.json` holds `executablePath` for the
  resolved path (see [install-locations.md](install-locations.md)).
- Invoking a URL while Hub runs logs a second-instance event with `protocolUrlSource: 'sharedData'`
  and "protocol URL forwarded to protocol handler". Normal starts log
  "deeplink: argv carries no unityhub:// link" (see
  [running indicators](config-and-logs.md#running-hub)).
- `user-settings.json` carries `deeplink.alwaysForwardToSingleEditor = true` here; this is a local
  observation, not a default (see [config-and-logs.md](config-and-logs.md)).

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
