#!/usr/bin/env pwsh
# Pre-commit checks, formerly .pre-commit-config.yaml (pre-commit stage).
# Hook mode checks the staged files; `--all-files` checks every tracked file (just check).
# Fixers rewrite the working tree and exit 1 without staging anything, as before.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'pins.ps1')

$root = "$(git rev-parse --show-toplevel)".Trim()
Set-Location $root

$allFiles = @($args) -contains '--all-files' -or @($args) -contains '-AllFiles'
$latin1 = [System.Text.Encoding]::Latin1
# Vendored skills and rules stay byte-for-byte as installed (hashes in skills-lock.json).
$vendored = '^\.claude/(skills/(add-dock-band|add-extension-settings|microsoft-code-reference|microsoft-docs|msstore-cli|publish-extension)|rules)/'
$failed = $false

function Get-Files {
    if ($allFiles) { @(git ls-files) } else { @(git diff --cached --name-only --diff-filter=ACMR) }
}

function Test-Tracked([string]$f) {
    return (Test-Path -LiteralPath $f -PathType Leaf)
}

# Git's own rule: a NUL byte in the first 8000 bytes means binary.
function Test-Text([string]$f) {
    $fs = [System.IO.File]::OpenRead($f)
    try {
        $buf = New-Object byte[] 8000
        $n = $fs.Read($buf, 0, $buf.Length)
    } finally {
        $fs.Dispose()
    }
    for ($i = 0; $i -lt $n; $i++) { if ($buf[$i] -eq 0) { return $false } }
    return $true
}

# Latin-1 round-trips every byte, so the fixers never change the encoding.
function Read-Raw([string]$f) { $latin1.GetString([System.IO.File]::ReadAllBytes($f)) }
function Write-Raw([string]$f, [string]$t) { [System.IO.File]::WriteAllBytes($f, $latin1.GetBytes($t)) }

function Report([string]$hook, [string]$message) {
    Write-Host "$hook`: $message"
    $script:failed = $true
}

$files = Get-Files

# --- Fixers: trailing whitespace, final newline ------------------------------------------
foreach ($f in $files) {
    if ($f -match $vendored) { continue }
    if (-not (Test-Tracked $f)) { continue }
    if (-not (Test-Text $f)) { continue }

    $t = Read-Raw $f
    $t2 = [regex]::Replace($t, '[ \t]+(?=\r?\n|\z)', '')
    if ($t2 -ne $t) {
        Write-Raw $f $t2
        $t = $t2
        Report 'trailing-whitespace' "fixed $f (not staged)"
    }
    if ($t.Length -gt 0) {
        $nl = if ($t.Contains("`r`n")) { "`r`n" } else { "`n" }
        $t3 = $t.TrimEnd([char[]]@("`r", "`n")) + $nl
        if ($t3 -ne $t) {
            Write-Raw $f $t3
            Report 'end-of-file' "fixed $f (not staged)"
        }
    }
}

# --- check-merge-conflict ----------------------------------------------------------------
foreach ($f in $files) {
    if (-not (Test-Tracked $f)) { continue }
    if (-not (Test-Text $f)) { continue }
    if ((Read-Raw $f) -match '(?m)^(<<<<<<< |=======\r?$|>>>>>>> )') {
        Report 'check-merge-conflict' "$f contains a merge conflict marker"
    }
}

# --- check-case-conflict -----------------------------------------------------------------
# Index plus tracked paths: a new Foo.cs collides with a tracked foo.cs even before it is added.
$caseFiles = @(@(git ls-files) + $files | Select-Object -Unique)
$caseGroups = @($caseFiles | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })
foreach ($g in $caseGroups) {
    Report 'check-case-conflict' "case-insensitive collision: $($g.Group -join ', ')"
}

# --- check-json --------------------------------------------------------------------------
foreach ($f in $files) {
    if ($f -notmatch '\.json$') { continue }
    if (-not (Test-Tracked $f)) { continue }
    try {
        $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($f)).TrimStart([char]0xFEFF)
        [System.Text.Json.JsonDocument]::Parse($text) | Out-Null
    } catch {
        Report 'check-json' "$f`: $($_.Exception.Message)"
    }
}

# --- check-xml ---------------------------------------------------------------------------
foreach ($f in $files) {
    if ($f -notmatch '\.(xml|csproj|props|targets|appxmanifest)$') { continue }
    if (-not (Test-Tracked $f)) { continue }
    $doc = New-Object System.Xml.XmlDocument
    try {
        $doc.Load((Resolve-Path -LiteralPath $f).Path)
    } catch {
        Report 'check-xml' "$f`: $($_.Exception.Message)"
    }
}

# --- actionlint (pinned $ActionlintVersion) ----------------------------------------------
$workflows = @($files | Where-Object { $_ -match '^\.github/workflows/.+\.ya?ml$' })
if ($workflows.Count -gt 0) {
    if (-not (Get-Command actionlint -ErrorAction SilentlyContinue)) {
        Report 'actionlint' "not found; install actionlint $ActionlintVersion (just doctor)"
    } else {
        $installed = [regex]::Match("$(actionlint --version 2>$null | Select-Object -First 1)", '\d+\.\d+\.\d+').Value
        if (-not (Test-ToolAtLeast $installed $ActionlintVersion)) {
            Report 'actionlint' "installed $installed is older than the $ActionlintVersion pin (just doctor)"
        } else {
            actionlint @workflows
            if ($LASTEXITCODE) { Report 'actionlint' "failed on $($workflows -join ', ')" }
        }
    }
}

# --- gitleaks (pinned $GitleaksVersion) --------------------------------------------------
if (-not (Get-Command gitleaks -ErrorAction SilentlyContinue)) {
    Report 'gitleaks' "not found; install gitleaks $GitleaksVersion (just doctor)"
} else {
    $installed = "$(gitleaks version 2>$null)".Trim()
    if (-not (Test-ToolAtLeast $installed $GitleaksVersion)) {
        Report 'gitleaks' "installed $installed is older than the $GitleaksVersion pin (just doctor)"
    } else {
        # Staged mode checks the index; --all-files scans the working tree, not git history.
        if ($allFiles) { gitleaks dir . -c .gitleaks.toml --no-banner --redact } else { gitleaks git --staged -c .gitleaks.toml --no-banner --redact }
        if ($LASTEXITCODE) { Report 'gitleaks' 'found a secret' }
    }
}

# --- csharpier (repo-wide) ---------------------------------------------------------------
if ($allFiles -or @($files | Where-Object { $_ -match '\.cs$' }).Count -gt 0) {
    dotnet tool run csharpier check .
    if ($LASTEXITCODE) { Report 'csharpier' 'check . failed' }
}

if ($failed) { exit 1 }
