# Initializes the gitignored vendor/ checkouts agents read as upstream source.
# Sparse + blobless: only the paths below are materialized; nothing here is a build input.
# PowerToys is pinned to the release matching the locally installed app: Command Palette ships
# with PowerToys, so that tag is the source the installed host actually runs.
param([switch]$Update)

$ErrorActionPreference = 'Stop'

$vendor = Join-Path (Join-Path $PSScriptRoot '..') 'vendor'

# The installed winget/MSIX version X.Y.Z.W maps to upstream tag vX.Y.Z. Ignore the stale
# %LOCALAPPDATA%\PowerToys copy: it is not the app that supplies the installed host.
function Get-LocalPowerToysRef([string]$url) {
    $exe = Join-Path $env:ProgramFiles 'PowerToys\PowerToys.exe'
    $version = ''
    if (Test-Path -LiteralPath $exe) {
        $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    } else {
        $app = Get-AppxPackage -Name 'Microsoft.PowerToys' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($app) { $version = $app.Version.ToString() }
    }
    $parts = $version -split '\.'
    if ($parts.Count -lt 3) { return $null }
    $ref = "v$($parts[0]).$($parts[1]).$($parts[2])"
    git ls-remote --exit-code --tags $url ("refs/tags/{0}" -f $ref) *> $null
    if ($LASTEXITCODE) { return $null }
    return $ref
}

$powerToysUrl = 'https://github.com/microsoft/PowerToys.git'
$powerToysRef = Get-LocalPowerToysRef $powerToysUrl
if (-not $powerToysRef) { 'warn     PowerToys release tag unresolved; PowerToys vendor tracks main' }

# Add a repo here when agents start consulting another upstream source. Keep the path list
# narrow: a whole PowerToys cmdpal module checkout is large and mostly unused.
$repos = @(
    [pscustomobject]@{
        Name  = 'PowerToys'
        Url   = $powerToysUrl
        Ref   = $powerToysRef
        Paths = @(
            'src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions'
            'src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit'
            'src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension'
            'src/modules/cmdpal/ext/SamplePagesExtension'
            'src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Apps'
            'src/modules/cmdpal/Microsoft.CmdPal.Common'
            'src/modules/cmdpal/Microsoft.CmdPal.UI'
            'src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels'
        )
    }
)

function Assert-Git([string]$what) {
    if ($LASTEXITCODE) { throw "$what failed (exit $LASTEXITCODE)" }
}

New-Item -ItemType Directory -Force -Path $vendor | Out-Null

foreach ($repo in $repos) {
    $dir = Join-Path $vendor $repo.Name
    $ref = $repo.Ref

    if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
        if (-not $Update) {
            "present  $($repo.Name) -> $dir (use -Update to refresh)"
            continue
        }
        if ($ref) {
            "updating $($repo.Name) -> $ref (local PowerToys release)"
            git -C $dir fetch --depth 1 origin ("refs/tags/{0}:refs/tags/{0}" -f $ref)
            Assert-Git "fetch $($repo.Name) $ref"
            git -C $dir checkout --detach $ref
            Assert-Git "checkout $($repo.Name) $ref"
        } else {
            "updating $($repo.Name) -> main"
            git -C $dir fetch --depth 1 origin HEAD
            Assert-Git "fetch $($repo.Name)"
            git -C $dir merge --ff-only FETCH_HEAD
            Assert-Git "merge $($repo.Name)"
        }
    } else {
        if (Test-Path -LiteralPath $dir) { throw "$dir exists but is not a git checkout; remove it first" }
        "cloning  $($repo.Name) -> $dir $(if ($ref) { "at $ref" } else { '' })"
        if ($ref) {
            git -C $vendor clone --filter=blob:none --sparse --depth 1 --branch $ref $repo.Url $repo.Name
        } else {
            git -C $vendor clone --filter=blob:none --sparse --depth 1 $repo.Url $repo.Name
        }
        Assert-Git "clone $($repo.Name)"
    }

    # Re-assert the path list on every run: the spec may have grown since the last init.
    git -C $dir sparse-checkout set --cone @($repo.Paths)
    Assert-Git "sparse-checkout set $($repo.Name)"
    "paths    $($repo.Name): $((git -C $dir sparse-checkout list) -join ' | ')"
    $head = (git -C $dir rev-parse --short HEAD)
    "ref      $($repo.Name): $head $(if ($ref) { "at $ref" } else { 'at main' })"
}
