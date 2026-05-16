# Contributing to Command Palette (CmdPal) Unity Extension

Start here if you want to work on the project.

## Read first

- `README.md` for the project overview and installation notes
- `docs/DEVELOPMENT.md` for build and debug flow
- `docs/PUBLISHING.md` for packaging and release flow
- `docs/TROUBLESHOOTING.md` for common issues

## What to keep in mind

- Keep changes small and focused.
- Preserve manifest / CLSID consistency between `UnityExtension.cs` and `Package.appxmanifest`.
- Keep code trim-safe and AOT-compatible.
- Avoid reflection-heavy patterns unless the upstream SDK requires them.
- Do not hardcode package versions in project files if centralized package management is used.

## Workflow

1. Make your change.
2. Build locally.
3. Test in PowerToys.
4. Verify publish behavior if the change affects deployment.
5. Check trimming, nullable, and packaging diagnostics.

## Pull requests

- Describe what changed and why.
- Mention any packaging or debug impact.
- Include manual validation notes when relevant.

## Source of truth

Use the latest Microsoft Learn / PowerToys docs for Command Palette, packaging, publishing, and debugging.
