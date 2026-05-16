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

### GitHub Releases release flow

The repository uses a tag-triggered workflow on `v*` tags. On release, GitHub Actions:

1. builds the MSIX bundle into `UnityExtension\BundleArtifacts`
2. restores the reusable local-dev PFX from secrets
3. signs the `.msixbundle`
4. verifies the signature
5. uploads the bundle and the public `.cer` as release assets

### Local-dev tester trust flow

For sideloading tests, keep one reusable signing cert and store the private key in GitHub Actions secrets:

- private key: store in GitHub Actions secrets as `MSIX_PFX_BASE64`
- password: store in GitHub Actions secrets as `MSIX_PFX_PASSWORD`
- public certificate: keep `UnityExtension\BundleArtifacts\Maoyeedy-MSIX-LocalDev.cer` for testers

If you need to create or refresh the secrets from a local `.pfx`, run:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Signing\UnityExtension-CI-Signing.pfx")) |
  gh secret set MSIX_PFX_BASE64

gh secret set MSIX_PFX_PASSWORD
```

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
