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

if (-not $rows) {
    'No editors found under HKCU/HKLM SOFTWARE\Unity Technologies\Installer.'
    exit 0
}

$rows | Sort-Object Version | Format-Table -AutoSize
