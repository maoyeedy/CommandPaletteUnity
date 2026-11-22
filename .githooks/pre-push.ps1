#!/usr/bin/env pwsh
# Pre-push checks, formerly .pre-commit-config.yaml (pre-push stage).
# Git passes "<local ref> <local sha> <remote ref> <remote sha>" per pushed ref on stdin.

$ErrorActionPreference = 'Stop'
$root = "$(git rev-parse --show-toplevel)".Trim()
Set-Location $root
$failed = $false

function Get-ChangedFiles([string]$Local, [string]$Remote) {
    if ($Remote -match '^0+$') {
        # New branch: compare its commits against master, else against its parent.
        $base = $null
        if (git rev-parse --verify --quiet origin/master) { $base = "$(git merge-base $Local origin/master 2>$null)".Trim() }
        if (-not $base -and (git rev-parse --verify --quiet "$Local^")) { $base = "$(git rev-parse "$Local^")".Trim() }
        if ($base) { return @(git diff --name-only --diff-filter=ACMR "$base..$Local") }
        return @(git diff-tree --no-commit-id --name-only -r $Local)
    }
    return @(git diff --name-only --diff-filter=ACMR "$Remote..$Local")
}

$changed = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($line in @($input)) {
    $parts = "$line".Trim() -split '\s+'
    if ($parts.Count -lt 4) { continue }
    foreach ($f in Get-ChangedFiles $parts[1] $parts[3]) { if ($f) { [void]$changed.Add($f) } }
}

if (@($changed | Where-Object { $_ -match '\.cs$' }).Count -gt 0) {
    dotnet format analyzers UnityExtension/UnityExtension.csproj --verify-no-changes
    if ($LASTEXITCODE) { $failed = $true }
}

# Exits 0 even when vulnerabilities are listed; keep that (see docs/DEPENDENCIES.md).
if (@($changed | Where-Object { $_ -match '\.csproj$' }).Count -gt 0) {
    dotnet list UnityExtension/UnityExtension.csproj package --vulnerable --include-transitive
    if ($LASTEXITCODE) { $failed = $true }
}

if ($failed) { exit 1 }
