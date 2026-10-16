# Publishing Guide

## Source of truth

Use latest Microsoft Learn / PowerToys docs for packaging + publishing.

## Package build

This is the same sequence CI runs. Restore first, then build with `--no-restore`; the unsigned build avoids `APPX0101` ("A signing key is required"), and you sign afterwards.

```bash
dotnet restore UnityExtension/UnityExtension.csproj
dotnet build UnityExtension/UnityExtension.csproj --no-restore \
  -c Release -p:Platform=x64 \
  -p:GenerateAppxPackageOnBuild=true \
  -p:AppxBundle=Always \
  -p:AppxBundlePlatforms="x64|arm64" \
  -p:AppxPackageSigningEnabled=false \
  -p:SuppressSdkNoise=false
```

Output (repo root, git-ignored): `AppPackages/UnityExtension_<version>.0_Test/UnityExtension_<version>.0_x64_arm64.msixbundle`

`win-x64.pubxml` and `win-arm64.pubxml` must set the same properties. If only one sets `PublishReadyToRun`, restore skips the other architecture's ReadyToRun runtime pack and the build fails with `NETSDK1094`.

Add `-p:TrimmerSingleWarn=false` to see every trim/AOT warning before submission. To sign for sideloading, see "Signing" below.

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

Build unsigned (see "Package build"), then sign the bundle with a self-signed cert whose `Subject` equals the manifest `Publisher` exactly (`CN=<GUID>`). Windows only installs it if that cert is trusted in `Cert:\LocalMachine\TrustedPeople`; importing there and verifying the signature both need an **elevated** shell (`CurrentUser` trust is not enough). Sideload with `Add-AppxPackage -Path <bundle> -ForceApplicationShutdown`.

Building with signing on (no `AppxPackageSigningEnabled=false`) fails with `APPX0101` unless you pass `-p:PackageCertificateThumbprint=<thumb>`; `GenerateTemporaryStoreCertificate` in the `.csproj` does not cover the bundle build.

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

Build with matching cert (or build unsigned and sign afterwards, as above):

```powershell
$thumb = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like '*<PublisherGUID>*' } | Select-Object -First 1 -ExpandProperty Thumbprint
dotnet build UnityExtension/UnityExtension.csproj --no-restore -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always -p:AppxBundlePlatforms="x64|arm64" -p:SuppressSdkNoise=true -p:PackageCertificateThumbprint=$thumb
```

## Install for testing

Remove old package first:

```powershell
Get-AppxPackage *CommandPalette-Unity* | Remove-AppxPackage
```

Install the signed bundle (the cert must be trusted, see "Signing"):

```powershell
Add-AppxPackage -Path "AppPackages\UnityExtension_*\UnityExtension_*.msixbundle" -ForceApplicationShutdown
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

The workflow's `PACKAGE_DIR` must stay absolute (`${{ github.workspace }}\...`): MSBuild resolves a relative `AppxPackageDir` against the project folder, so the bundle would otherwise land where the "Find MSIX bundle" step does not look. The verify step imports the `.cer` into `LocalMachine\TrustedPeople` first, because a clean runner does not trust the self-signed cert.

**Dry run:** `gh workflow run release-msix.yml --ref <branch>` (or Actions > Release MSIX > Run workflow) builds, signs, verifies and uploads the artifact without creating a release or touching the Store. Use it to test workflow changes (including action bumps) before tagging; the GitHub release and `store-publish` steps run only for tags.

**Re-pointing a tag:** the `protect-release-tags` ruleset blocks deleting or updating `v*` tags, and a run that fails at startup cannot be retried. To redo a tag, temporarily set the ruleset's enforcement to Disabled (repo Settings > Rules > Rulesets, not the read-only Rules page), then `git tag -f vX.Y.Z && git push origin :refs/tags/vX.Y.Z && git push origin vX.Y.Z`, then set it back to Active. Prefer the dry run so this is rarely needed.

### Microsoft Store updates (automated)

The first Store submission was done in Partner Center; updates can use the `store-publish` job in `release-msix.yml`. It is off until you opt in.

One-time setup:

1. Partner Center > Account settings > Tenants: create or associate a Microsoft Entra tenant, then sign in again.
2. Account settings > User management > Microsoft Entra applications: add an app (any name and reply URL, e.g. `https://localhost`), give it the **Manager** role, then **Add new key**. Copy the client ID and the client secret (shown once).
3. Seller ID: Account settings > Organization profile > Legal info.
4. Repo secrets: `PARTNER_CENTER_TENANT_ID`, `PARTNER_CENTER_SELLER_ID`, `PARTNER_CENTER_CLIENT_ID`, `PARTNER_CENTER_CLIENT_SECRET`.
5. Repo variable `STORE_PUBLISH=true` (set it to `false` to pause the job).
6. Settings > Environments > `microsoft-store`: add yourself as required reviewer so each submission waits for approval.
7. Locally: `msstore reconfigure --tenantId ... --sellerId ... --clientId ... --clientSecret ...`. The interactive form can fail with "Error while retrieving Organization"; the flags work.
8. Settings > Actions > General: the repo restricts allowed actions, so add `microsoft/microsoft-store-apppublisher@*` to the allow list. Without it the whole release workflow fails at startup (`startup_failure`, zero jobs, no rerun possible), including the build job.

On a `v*` tag the job downloads the built bundle and runs `msstore publish <bundle> -id 9NVSJZ72LTZ3`, then `msstore submission poll`. The poll step returns once the submission is committed and in certification; it does not wait for it to go live, so a green run means "accepted", not "published". Check `msstore submission status 9NVSJZ72LTZ3` or Partner Center for the result. The Store re-signs the package, so the self-signed PFX does not matter. Only free apps are supported by the CLI; the version must be higher than the last published one, so bump the version before tagging. A `.msixbundle` path works with `publish` (verified with `--noCommit`).

Before tagging a release:

1. Bump the version via a PR and merge it, so `master` carries both the new version and the `store-publish` job.
2. Make sure no draft is open in the Partner Center web UI, and that the new version is higher than the last published one.
3. Tag `vX.Y.Z` from `master`. The job pauses in the `microsoft-store` environment until a required reviewer approves; rejecting it skips the Store but keeps the GitHub release.

Gotchas:

- The API cannot touch a draft created in the Partner Center web UI. If one is open, `publish` stops on "Ingestion API can only update, delete, and commit submissions that are created through the API", and in CI it would hang. Delete or submit the web draft first, and do not leave drafts open in the portal.
- `publish` discards any existing API draft, so run it before editing listing metadata.
- Never put the client secret in chat, docs, or commits; keep it in the GitHub secret and your password manager.

Local equivalent: `msstore publish <bundle> -id 9NVSJZ72LTZ3 --noCommit` (draft), `msstore submission get 9NVSJZ72LTZ3`, `msstore submission publish 9NVSJZ72LTZ3`. `msstore submission delete 9NVSJZ72LTZ3` removes an API draft.

### WinGet

The Store listing is already installable with `winget install 9NVSJZ72LTZ3 -s msstore`; no extra setup is needed. Submitting to `microsoft/winget-pkgs` (the `windows-commandpalette-extension` tag route in the `publish-extension` skill) needs a separate unpackaged EXE installer (Inno Setup, `WindowsPackageType=None`) and is not set up.

## CmdPal gallery submission

Gallery metadata lives in the `microsoft/CmdPal-Extensions` repo, not here: `extensions/maoyeedy/cmdpal-unity-extension/` (`extension.json`, `icon.png`, `screenshots/`). The install source is `msstore` with ID `9NVSJZ72LTZ3`. Update that repo when the title, description, icon or screenshots change.
