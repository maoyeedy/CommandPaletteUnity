---
name: release
description: >-
  Cut a new app release of the Unity Command Palette extension: sync the version across the csproj and Package.appxmanifest, validate with a release build, then tag so CI signs and publishes the GitHub release. Use for requests to release, tag, publish or bump the app's own version, or when a version mismatch breaks the tag build. Not for NuGet, tool or pre-commit updates (use bump-deps).
---

# Release

The tag triggers `.github/workflows/release-msix.yml`, which validates versions, builds, signs with the CI secret and creates the GitHub release with the bundle and `.cer`. When repo variable `STORE_PUBLISH` is `true`, the `store-publish` job then waits for reviewer approval in the `microsoft-store` environment and submits the same bundle with `msstore` (details and gotchas in `docs/PUBLISHING.md`). Local signing is only for sideload testing.

## 1. Sync the version

Given `X.Y.Z`, set all four of these (CI fails the tag build if any disagrees):

| File | Field | Value |
|------|-------|-------|
| `UnityExtension/UnityExtension.csproj` | `<Version>` | `X.Y.Z` |
| same | `<AssemblyVersion>`, `<FileVersion>` | `X.Y.Z.0` |
| `UnityExtension/Package.appxmanifest` | `Identity Version` | `X.Y.Z.0` |
| git tag | `vX.Y.Z` | |

The fourth segment stays `.0` (Store rule). Then grep tracked files (not `bin/`, `obj/`, `AppPackages/`) for the old version in docs and examples and update what should track the release. Never touch `Identity Name` or `Publisher`.

## 2. Validate

Delete `bin`, `obj` and `AppPackages` if identity, name or publish profiles changed, then run the restore + `--no-restore` unsigned bundle build from `docs/PUBLISHING.md` (the PR CI job does not build the bundle, so this is the only pre-tag packaging check). A bundle at repo-root `AppPackages/UnityExtension_<X.Y.Z.0>_Test/UnityExtension_<X.Y.Z.0>_x64_arm64.msixbundle` means it worked. Optionally sideload-test it via the `deploy` skill.

The version must be higher than the one last published to the Store (`winget show` does not report it; ask the user or check Partner Center), and no draft may be open in the Partner Center web UI.

## 3. Commit and tag

Commit the version change on a branch (Conventional Commits, e.g. `chore: bump version to X.Y.Z`; mention notable merged PRs the release ships), open a PR and have it merged, then tag `master` so the tag commit contains the release workflow as it should run. Pushing the tag publishes a public release, triggers CI and, with `STORE_PUBLISH=true`, queues a Store submission, so get explicit user approval before `git push` and `git push origin vX.Y.Z`. Check `git remote get-url origin` first. Afterwards watch the run with `gh run watch`; the `store-publish` job pauses for the user's approval in GitHub, which the agent does not give.

## Report

Version applied, files changed, build result, and the commands run or left for the user to approve.
