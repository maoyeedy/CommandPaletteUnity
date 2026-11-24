# Dev-only, read-only: list editors from the same registry keys EditorParser reads.
# Does not start Unity, run the Unity CLI, or write to the registry.
$ErrorActionPreference = 'Stop'

$regPaths = @(
    'HKCU:\SOFTWARE\Unity Technologies\Installer',
    'HKLM:\SOFTWARE\Unity Technologies\Installer'
)

$rows = @()
foreach ($regPath in $regPaths) {
    if (-not (Test-Path -LiteralPath $regPath)) { continue }
    Get-ChildItem -LiteralPath $regPath -ErrorAction SilentlyContinue | ForEach-Object {
        $props = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
        $version = $props.Version
        $location = $props.'Location x64'
        if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($location)) { return }
        $exe = Join-Path $location 'Editor\Unity.exe'
        $rows += [PSCustomObject]@{
            Version     = $version
            InstallPath = $location
            Executable  = $exe
            ExeExists   = (Test-Path -LiteralPath $exe)
            Hive        = $regPath.Split(':')[0]
        }
    }
}

# EditorParser ignores a registry entry when <install path>\Editor\Unity.exe is missing.
$usable = @($rows | Where-Object { $_.ExeExists })
$skipped = @($rows | Where-Object { -not $_.ExeExists })

if (-not $usable) {
    'No usable editors found under HKCU/HKLM SOFTWARE\Unity Technologies\Installer.'
    if ($skipped) { "Skipped $($skipped.Count) stale registry entry/entries." }
    exit 0
}

$usable | Sort-Object Version |
    Select-Object Version, InstallPath, Executable, Hive |
    Format-Table -AutoSize
if ($skipped) {
    "Skipped $($skipped.Count) stale registry entry/entries without <install path>\Editor\Unity.exe (EditorParser ignores these)."
}
