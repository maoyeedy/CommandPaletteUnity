---
name: release
description: >-
  Cut a new app release of the Unity Command Palette extension: sync the version across the csproj and Package.appxmanifest, validate with a release build, then tag so CI signs and publishes the GitHub release. Use for requests to release, tag, publish or bump the app's own version, or when a version mismatch breaks the tag build. Not for NuGet, tool or pre-commit updates (use bump-deps).
---

# Release

The tag triggers `.github/workflows/release-msix.yml`, which validates versions, builds, signs with the CI secret and creates the GitHub release with the bundle and `.cer`. Local signing is only for sideload testing.

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

Delete `bin`, `obj` and `AppPackages` if identity or name changed, then run the release bundle build from `docs/PUBLISHING.md`. A bundle at `AppPackages/UnityExtension_<X.Y.Z.0>_Test/UnityExtension_<X.Y.Z.0>_x64_arm64.msixbundle` means it worked. Optionally sideload-test it via the `deploy` skill.

## 3. Commit and tag

Commit the version change (Conventional Commits, e.g. `chore: bump version to X.Y.Z`). Pushing the tag publishes a public release and triggers CI, so get explicit user approval before `git push` and `git push origin vX.Y.Z`. Check `git remote get-url origin` first. Afterwards watch the run with `gh run watch`.

## Report

Version applied, files changed, build result, and the commands run or left for the user to approve.
