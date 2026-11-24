<#
.SYNOPSIS
  Local development signing for the Unity Command Palette MSIX/MSIXBUNDLE.

.DESCRIPTION
  Creates or reuses a self-signed code-signing certificate whose subject matches
  Package.appxmanifest Identity Publisher, trusts the public certificate locally,
  signs the MSIX/MSIXBUNDLE with SignTool, and verifies the result.

  This is for local development / sideload testing only. Do not use self-signed
  certificates for public distribution.

.EXAMPLE
  .\Sign-LocalMsix.ps1

.EXAMPLE
  .\Sign-LocalMsix.ps1 -BundlePath ".\AppPackages\UnityExtension_0.1.1.0_Test\UnityExtension_0.1.1.0_x64_arm64.msixbundle"

.EXAMPLE
  .\Sign-LocalMsix.ps1 -ExportPfx -PfxOutputPath "C:\Signing\Maoyeedy-MSIX.pfx"
#>

[CmdletBinding()]
param(
    [string]$BundlePath,

    [string]$ManifestPath,

    [string]$Subject = "CN=Yidi Mao",

    [string]$FriendlyName = "Maoyeedy MSIX Local Dev Signing",

    [string]$CerOutputPath,

    [switch]$ExportPfx,

    [string]$PfxOutputPath,

    [switch]$ForceNewCertificate,

    [ValidateSet("LocalMachine", "CurrentUser", "None")]
    [string]$TrustStore = "LocalMachine",

    [switch]$NoTimestamp,

    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "`n== $Message ==" -ForegroundColor Cyan
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-RepoProjectRoot {
    $scriptDir = $PSScriptRoot

    if ($scriptDir) {
        $candidate = Resolve-Path (Join-Path $scriptDir "..")
        if (Test-Path (Join-Path $candidate "Package.appxmanifest")) {
            return $candidate.Path
        }
    }

    $current = Resolve-Path "."
    if (Test-Path (Join-Path $current "Package.appxmanifest")) {
        return $current.Path
    }

    $nested = Join-Path $current "UnityExtension"
    if (Test-Path (Join-Path $nested "Package.appxmanifest")) {
        return (Resolve-Path $nested).Path
    }

    throw "Could not resolve the UnityExtension project root. Pass -ManifestPath and -BundlePath explicitly."
}

function Resolve-LatestBundlePath {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $searchRoots = @(
        (Join-Path $ProjectRoot "AppPackages"),
        (Join-Path $ProjectRoot "BundleArtifacts"),
        (Join-Path $ProjectRoot "bin")
    ) | Where-Object { Test-Path $_ }

    $bundle = $searchRoots |
        ForEach-Object { Get-ChildItem -Path $_ -Recurse -File -Include *.msixbundle,*.msix -ErrorAction SilentlyContinue } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $bundle) {
        throw "No .msixbundle or .msix file found under AppPackages, BundleArtifacts, or bin. Pass -BundlePath explicitly."
    }

    return $bundle.FullName
}

function Resolve-SignToolPath {
    $sdkBinRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $sdkBinRoot)) {
        throw "Windows SDK bin folder was not found: $sdkBinRoot. Install the Windows SDK, then retry."
    }

    $tool = Get-ChildItem $sdkBinRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw "signtool.exe was not found under $sdkBinRoot. Install the Windows SDK, then retry."
    }

    return $tool.FullName
}

function Get-ManifestPublisher {
    param([Parameter(Mandatory = $true)][string]$Path)

    [xml]$manifest = Get-Content -Path $Path -Raw
    $identity = $manifest.Package.Identity
    if (-not $identity -or -not $identity.Publisher) {
        throw "Could not read Identity Publisher from manifest: $Path"
    }

    return [string]$identity.Publisher
}

function Get-OrCreateSigningCertificate {
    param(
        [Parameter(Mandatory = $true)][string]$CertSubject,
        [Parameter(Mandatory = $true)][string]$CertFriendlyName,
        [Parameter(Mandatory = $true)][bool]$CreateNew
    )

    $codeSigningEku = "1.3.6.1.5.5.7.3.3"

    if (-not $CreateNew) {
        $existing = Get-ChildItem "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Subject -eq $CertSubject -and
                $_.HasPrivateKey -and
                $_.NotAfter -gt (Get-Date) -and
                ($_.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningEku })
            } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1

        if ($existing) {
            Write-Host "Reusing certificate: $($existing.Thumbprint) ($($existing.NotAfter.ToString('u')))"
            return $existing
        }
    }

    Write-Host "Creating self-signed code-signing certificate in Cert:\CurrentUser\My..."
    return New-SelfSignedCertificate -Type Custom `
        -Subject $CertSubject `
        -FriendlyName $CertFriendlyName `
        -KeyUsage DigitalSignature `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @(
            "2.5.29.37={text}$codeSigningEku",
            "2.5.29.19={text}false"
        )
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory = $true)][string]$FailureMessage,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

$projectRoot = Resolve-RepoProjectRoot

if (-not $ManifestPath) {
    $ManifestPath = Join-Path $projectRoot "Package.appxmanifest"
}

if (-not (Test-Path $ManifestPath)) {
    throw "Manifest not found: $ManifestPath"
}

$ManifestPath = (Resolve-Path $ManifestPath).Path

if (-not $BundlePath) {
    $BundlePath = Resolve-LatestBundlePath -ProjectRoot $projectRoot
}

if (-not (Test-Path $BundlePath)) {
    throw "Bundle not found: $BundlePath"
}

$BundlePath = (Resolve-Path $BundlePath).Path

if (-not $CerOutputPath) {
    $CerOutputPath = Join-Path $projectRoot "BundleArtifacts\Maoyeedy-MSIX-LocalDev.cer"
}

if (-not $PfxOutputPath) {
    $PfxOutputPath = Join-Path $projectRoot "BundleArtifacts\Maoyeedy-MSIX-LocalDev.pfx"
}

$cerDir = Split-Path -Parent $CerOutputPath
if ($cerDir -and -not (Test-Path $cerDir)) {
    New-Item -ItemType Directory -Path $cerDir | Out-Null
}

$pfxDir = Split-Path -Parent $PfxOutputPath
if ($ExportPfx -and $pfxDir -and -not (Test-Path $pfxDir)) {
    New-Item -ItemType Directory -Path $pfxDir | Out-Null
}

Write-Step "Validating manifest publisher"
$manifestPublisher = Get-ManifestPublisher -Path $ManifestPath
Write-Host "Manifest : $ManifestPath"
Write-Host "Publisher: $manifestPublisher"
Write-Host "Subject  : $Subject"

if ($manifestPublisher -cne $Subject) {
    throw "Certificate subject must exactly match Package.appxmanifest Identity Publisher. Manifest has '$manifestPublisher', script has '$Subject'."
}

if ($TrustStore -eq "LocalMachine" -and -not (Test-IsAdministrator)) {
    throw "-TrustStore LocalMachine requires an elevated PowerShell session. Re-run as Administrator, or use -TrustStore CurrentUser."
}

Write-Step "Resolving signing certificate"
$cert = Get-OrCreateSigningCertificate -CertSubject $Subject -CertFriendlyName $FriendlyName -CreateNew $ForceNewCertificate.IsPresent
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Expires   : $($cert.NotAfter.ToString('u'))"

Write-Step "Exporting public certificate"
Export-Certificate -Cert $cert -FilePath $CerOutputPath -Force | Out-Null
Write-Host "CER: $CerOutputPath"

if ($ExportPfx) {
    Write-Step "Exporting private certificate as PFX"
    Write-Warning "Keep this PFX out of Git. It contains the private key."
    $securePwd = Read-Host "Enter password for PFX export" -AsSecureString
    Export-PfxCertificate -Cert $cert -FilePath $PfxOutputPath -Password $securePwd -Force | Out-Null
    Write-Host "PFX: $PfxOutputPath"
}

if ($TrustStore -ne "None") {
    Write-Step "Trusting public certificate"
    $storePath = "Cert:\$TrustStore\TrustedPeople"
    Import-Certificate -FilePath $CerOutputPath -CertStoreLocation $storePath | Out-Null
    Write-Host "Imported CER into $storePath"
}
else {
    Write-Host "Skipping certificate trust because -TrustStore None was specified."
}

Write-Step "Resolving SignTool"
$signtool = Resolve-SignToolPath
Write-Host "SignTool: $signtool"

Write-Step "Signing package"
Write-Host "Bundle: $BundlePath"

if ($NoTimestamp) {
    Invoke-NativeChecked -FailureMessage "SignTool signing failed." -Command {
        & $signtool sign /v /fd SHA256 /sha1 $cert.Thumbprint $BundlePath
    }
}
else {
    Invoke-NativeChecked -FailureMessage "SignTool signing failed." -Command {
        & $signtool sign /v /fd SHA256 /sha1 $cert.Thumbprint /tr $TimestampUrl /td SHA256 $BundlePath
    }
}

Write-Step "Verifying signature"
Invoke-NativeChecked -FailureMessage "SignTool verification failed." -Command {
    & $signtool verify /pa /v $BundlePath
}

Write-Step "Done"
Write-Host "Signed bundle: $BundlePath" -ForegroundColor Green
Write-Host "Test install:"
Write-Host "  Add-AppxPackage -Path `"$BundlePath`" -ForceApplicationShutdown"
