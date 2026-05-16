# Command Palette (CmdPal) Unity Extension

## Overview
Command Palette extension for opening Unity Hub recent projects.

![Screenshot](docs/assets/Screenshot.png)

## Installation

### Requirements
- [PowerToys](https://learn.microsoft.com/en-us/windows/powertoys/) with Command Palette enabled
- Unity Hub with recent projects history

> Unity Hub not required in background - extension launches editor directly.

### Via GitHub

Download releases from [Releases page](https://github.com/maoyeedy/CmdPalUnityExtension/releases).

Before sideloading new bundle, remove current package:

```powershell
Get-AppxPackage *UnityForCmdPal* | Remove-AppxPackage
```

## Contributing and development

Start here: [`CONTRIBUTING.md`](CONTRIBUTING.md)

Local workflow:

1. Build: `dotnet build UnityExtension.sln -c Debug -p:Platform=x64`
2. Register: `Add-AppxPackage -Register` against debug `AppxManifest.xml`
3. Restart PowerToys

Supporting docs:

- [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
- [`docs/PUBLISHING.md`](docs/PUBLISHING.md)
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md)

## TODO

- [x] Settings to sort/filter list output
- [ ] Fallback dialog if editor not installed
- [ ] Open project with another Unity version
- [ ] Sub-command to list installed Unity versions + paths
- [ ] Overwrite timestamp in Unity Hub JSON
