# Publishing Guide

## Source of truth

Use latest Microsoft Learn / PowerToys docs for packaging + publishing.

## Package build

```bash
dotnet build UnityExtension/UnityExtension.csproj \
  -c Release -p:Platform=x64 \
  -p:GenerateAppxPackageOnBuild=true \
  -p:AppxBundle=Always \
  -p:AppxBundlePlatforms="x64|arm64" \
  -p:SuppressSdkNoise=true
```

Output: `AppPackages/<version>_Test/*.msixbundle`

For full trim/AOT warnings before submission:

```bash
# omit SuppressSdkNoise and add TrimmerSingleWarn=false
dotnet build UnityExtension/UnityExtension.csproj \
  -c Release -p:Platform=x64 \
  -p:GenerateAppxPackageOnBuild=true \
  -p:AppxBundle=Always \
  -p:AppxBundlePlatforms="x64|arm64" \
  -p:SuppressSdkNoise=false \
  -p:TrimmerSingleWarn=false
```

## Package identity

`Package.appxmanifest` Identity values **must** match Partner Center:

- `Name` — the reserved Partner Center identity name
- `Publisher` — `CN=<GUID>` from Partner Center
- `Version` — four-part, fourth segment always `.0`

`.csproj` properties mirror these:

```xml
<AppxPackageIdentityName>PartnerCenterName</AppxPackageIdentityName>
<AppxPackagePublisher>CN=<GUID></AppxPackagePublisher>
```

## Signing

### Local dev

`GenerateTemporaryStoreCertificate=True` in `.csproj` auto-creates a temp cert matching the manifest `Publisher`. No thumbprint needed for `dotnet build`.

### Store submission

The Store re-signs your package — a self-signed cert whose `Subject` matches the manifest `Publisher` is sufficient for build.

Create once:

```powershell
New-SelfSignedCertificate -Type Custom -KeyUsage DigitalSignature `
  -Subject "CN=<PublisherGUID>" `
  -FriendlyName "YourApp Store Signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
```

Build with matching cert:

```powershell
$thumb = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*<PublisherGUID>*' } | Select-Object -First 1 -ExpandProperty Thumbprint
dotnet build UnityExtension/UnityExtension.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|arm64" -p:SuppressSdkNoise=true -p:PackageCertificateThumbprint=$thumb
```

## Install for testing

Remove old package first:

```powershell
Get-AppxPackage *CommandPalette-Unity* | Remove-AppxPackage
```

Install the bundle:

```powershell
Add-AppxPackage -Path "AppPackages\UnityExtension_*\UnityExtension_*.msixbundle"
```

Reload Command Palette or restart PowerToys after install.

## Release checks

- manifest identity values match Partner Center
- CLSID matches across `UnityExtension.cs` + `Package.appxmanifest`
- cert `Subject` matches manifest `Publisher`
- x64 / arm64 outputs present in bundle
- extension loads after install
- debug deployment uses `Add-AppxPackage -Register` with `AppxManifest.xml`

## Distribution

Options: Microsoft Store, WinGet, or self-hosted GitHub Releases.

### GitHub Releases flow

Tag-triggered workflow on `v*` tags. GitHub Actions builds the MSIX bundle, signs with a PFX from secrets, and uploads the `.msixbundle` + public `.cer` as release assets.

For local-dev tester trust, keep one reusable signing cert and store the private key in GitHub Actions secrets:
- `MSIX_PFX_BASE64` — the `.pfx` as base64
- `MSIX_PFX_PASSWORD` — the PFX password
- `UnityExtension\BundleArtifacts\Maoyeedy-MSIX-LocalDev.cer` — public certificate for testers

## CmdPal gallery submission

Gallery metadata lives at:

- `gallery/maoyeedy/cmdpal-unity-extension/extension.json`
- `gallery/maoyeedy/cmdpal-unity-extension/icon.png`
- `gallery/maoyeedy/cmdpal-unity-extension/screenshots/01-main.png`

The `url` install source points at the GitHub Releases page. Update it if the release URL changes.
