# Initializes the gitignored vendor/ checkouts agents read as upstream source.
# Sparse + blobless: only the paths below are materialized; nothing here is a build input.
param([switch]$Update)

$ErrorActionPreference = 'Stop'

$vendor = Join-Path (Join-Path $PSScriptRoot '..') 'vendor'

# Add a repo here when agents start consulting another upstream source. Keep the path list
# narrow: a whole PowerToys cmdpal module checkout is large and mostly unused.
$repos = @(
    [pscustomobject]@{
        Name  = 'PowerToys'
        Url   = 'https://github.com/microsoft/PowerToys.git'
        Paths = @(
            'src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions'
            'src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit'
            'src/modules/cmdpal/ExtensionTemplate/TemplateCmdPalExtension'
            'src/modules/cmdpal/ext/SamplePagesExtension'
        )
    }
)

function Assert-Git([string]$what) {
    if ($LASTEXITCODE) { throw "$what failed (exit $LASTEXITCODE)" }
}

New-Item -ItemType Directory -Force -Path $vendor | Out-Null

foreach ($repo in $repos) {
    $dir = Join-Path $vendor $repo.Name

    if (Test-Path -LiteralPath (Join-Path $dir '.git')) {
        if (-not $Update) {
            "present  $($repo.Name) -> $dir (use -Update to refresh)"
            continue
        }
        "updating $($repo.Name)"
        git -C $dir fetch --depth 1 origin HEAD
        Assert-Git "fetch $($repo.Name)"
        git -C $dir merge --ff-only FETCH_HEAD
        Assert-Git "merge $($repo.Name)"
    } else {
        if (Test-Path -LiteralPath $dir) { throw "$dir exists but is not a git checkout; remove it first" }
        "cloning  $($repo.Name) -> $dir"
        git -C $vendor clone --filter=blob:none --sparse --depth 1 $repo.Url $repo.Name
        Assert-Git "clone $($repo.Name)"
    }

    # Re-assert the path list on every run: the spec may have grown since the last init.
    git -C $dir sparse-checkout set --cone @($repo.Paths)
    Assert-Git "sparse-checkout set $($repo.Name)"
    "paths    $($repo.Name): $((git -C $dir sparse-checkout list) -join ' | ')"
}
