# Compares runtime-shared packages against the latest PowerToys *release*
# (what users run), not PowerToys main. Build-only packages (SDK.BuildTools,
# CsWin32) may be newer and are not checked.
# Exit code = number of packages above PowerToys; with -Strict, packages
# behind PowerToys count too (used by the weekly CI run as a bump reminder).
param([switch]$Strict)

$ErrorActionPreference = 'Stop'
$ids = 'Microsoft.CommandPalette.Extensions', 'Microsoft.WindowsAppSDK', 'Microsoft.Windows.CsWinRT'

$tag = gh api repos/microsoft/PowerToys/releases/latest --jq .tag_name
if ($LASTEXITCODE -or -not $tag) { throw 'Could not read latest PowerToys release' }
$upstream = gh api "repos/microsoft/PowerToys/contents/Directory.Packages.props?ref=$tag" --jq .content
if ($LASTEXITCODE) { throw "Could not read Directory.Packages.props at $tag" }
$theirs = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($upstream -join '')))
$ours = Get-Content (Join-Path $PSScriptRoot '..' '..' 'Directory.Packages.props') -Raw

"PowerToys $tag"
$bad = 0
foreach ($id in $ids) {
    $re = 'Include="' + [regex]::Escape($id) + '"\s+Version="([^"]+)"'
    $o = [regex]::Match($ours, $re).Groups[1].Value
    $t = [regex]::Match($theirs, $re).Groups[1].Value
    $state = 'ok'
    if (-not $t) { $state = '(not pinned)' }
    elseif ($o) {
        $cmp = ([version]($o -replace '-.*')).CompareTo([version]($t -replace '-.*'))
        if ($cmp -gt 0) { $state = 'ABOVE RELEASE'; $bad++ }
        elseif ($cmp -lt 0) { $state = 'BEHIND (bump-deps)'; if ($Strict) { $bad++ } }
    }
    '{0,-40} ours {1,-16} PowerToys {2,-16} {3}' -f $id, $o, $t, $state
}
exit $bad
