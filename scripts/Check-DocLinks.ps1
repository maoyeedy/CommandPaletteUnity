# Dev-only, read-only: check relative Markdown links and heading anchors.
# Run with just docs-check. Windows-only, like the rest of this repo's dev scripts.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
# Scope: repo docs and root-level Markdown. Vendored .claude/skills are not ours to fix.
$files = @(Get-ChildItem -LiteralPath $root -File -Filter '*.md' -ErrorAction SilentlyContinue)
$docsDir = Join-Path $root 'docs'
if (Test-Path -LiteralPath $docsDir) {
    $files += Get-ChildItem -LiteralPath $docsDir -Recurse -File -Filter '*.md' -ErrorAction SilentlyContinue
}
$files = $files | Sort-Object FullName -Unique

$errors = 0
$checked = 0
foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    $text = $lines -join "`n"
    $matches = [regex]::Matches($text, '\[([^\]]+)\]\(([^)]+)\)')
    foreach ($match in $matches) {
        $target = $match.Groups[2].Value.Trim()
        if (-not $target -or $target -match '^[a-z]+:' -or $target.StartsWith('#')) { continue }
        $parts = $target.Split('#', 2)
        $relative = $parts[0]
        if (-not $relative) { continue }
        $full = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $relative))
        $checked++
        if (-not (Test-Path -LiteralPath $full)) {
            "BROKEN $($file.FullName): $target"
            $errors++
            continue
        }
        if ($parts.Count -eq 2 -and $parts[1]) {
            $anchor = $parts[1].ToLowerInvariant()
            $headingText = Get-Content -LiteralPath $full | Where-Object { $_ -match '^#{1,6}\s+' } | ForEach-Object {
                ($_ -replace '^#{1,6}\s+', '').Trim().ToLowerInvariant() -replace '[^\w\s-]', '' -replace '\s', '-'
            }
            if ($anchor -notin $headingText) {
                "BAD ANCHOR $($file.FullName): $target"
                $errors++
            }
        }
    }
}

"Checked $checked relative link(s) in $($files.Count) Markdown file(s)."
if ($errors) { "$errors broken link(s) or anchor(s)"; exit 1 }
'All relative links and anchors resolve.'
