# Publishing Guide

## Source of truth

Use latest Microsoft Learn / PowerToys docs for packaging + publishing.

## Local publish

```bash
dotnet publish UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64
dotnet publish UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:TrimmerSingleWarn=false
```

CsWinRT / linker noise suppressed by default for local publish commands. To inspect full trim output:

```bash
dotnet publish UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:TrimmerSingleWarn=false -p:SuppressSdkNoise=false
```

Keep default suppression for local smoke tests; turn off for warning review + compatibility checks.

Package directly:

```bash
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:TrimmerSingleWarn=false
```

Clean reinstall of packaged build - remove previous MSIX first:

```powershell
Get-AppxPackage *UnityForCmdPal* | Remove-AppxPackage
```

Install latest bundle:

```powershell
Add-AppxPackage -Path "AppPackages\UnityExtension_*\UnityExtension_*.msixbundle"
```

After install, reload Command Palette or restart PowerToys.

## Release checks

- manifest identity values correct
- CLSID matches across `UnityExtension.cs` + `Package.appxmanifest`
- signing configured correctly
- x64 / arm64 outputs correct if shipping both
- extension loads after install
- debug deployment uses `Add-AppxPackage -Register` with `AppxManifest.xml`
- if `GenerateAppxPackageOnBuild=true`, avoid forcing `WindowsPackageType=None`

## Distribution

Options: WinGet, Microsoft Store, or self-hosted GitHub Releases.
For local install, remove the existing package first:

```powershell
Get-AppxPackage *UnityForCmdPal* | Remove-AppxPackage
Add-AppxPackage -Path "AppPackages\UnityExtension_*\UnityExtension_*.msixbundle"
```

## CmdPal gallery submission prep

The gallery submission files are prepared under:

- `gallery/maoyeedy/cmdpal-unity-extension/extension.json`
- `gallery/maoyeedy/cmdpal-unity-extension/icon.png`
- `gallery/maoyeedy/cmdpal-unity-extension/screenshots/01-main.png`

The gallery metadata uses a `url` install source pointing at the GitHub Releases page. Update it if the release URL changes.
