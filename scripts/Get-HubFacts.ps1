# Dev-only, read-only: print the local Hub facts used in docs/reference/unity.
# Does not start Unity Hub, run the Unity CLI, or modify the registry.
# Output is masked to the placeholders used in the docs: no real paths, project
# names, hashes or package identities are printed. Do not paste raw local values into docs.
$ErrorActionPreference = 'Stop'

function Show-Section([string]$title) {
    ''
    "== $title =="
}

$hubDir = Join-Path $env:APPDATA 'UnityHub'

Show-Section 'Hub info'
$infoPath = Join-Path $hubDir 'hubInfo.json'
if (Test-Path -LiteralPath $infoPath) {
    $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
    "version       = $($info.version)"
    "executablePath= <hub package>\app\Unity Hub.exe"
} else {
    'No hubInfo.json in %APPDATA%\UnityHub'
}

Show-Section 'Running Hub process'
$hubProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity Hub.exe'" -ErrorAction SilentlyContinue)
if ($hubProcesses) {
    "process count = $($hubProcesses.Count)"
    'process name  = Unity Hub.exe'
    'executable    = <hub package>\app\Unity Hub.exe (path and command line masked)'
} else {
    'Not running'
}

Show-Section 'Running indicators'
foreach ($name in @('hub.db-wal', 'hub.db-shm')) {
    "$name = $(Test-Path -LiteralPath (Join-Path $hubDir $name))"
}
$runningLogs = @(Get-ChildItem -LiteralPath (Join-Path $hubDir 'logs') -Filter 'hub-gpui-running-*.json' -ErrorAction SilentlyContinue)
"hub-gpui-running logs = $($runningLogs.Count)"

Show-Section 'unityhub protocol registration'
$marker = 'Registry::HKEY_CLASSES_ROOT\unityhub'
if (Test-Path -LiteralPath $marker) {
    "marker keys = $((Get-Item -LiteralPath $marker).Property -join ', ')"
} else {
    'No HKCR\unityhub marker'
}
$protocolRoot = 'Registry::HKEY_CLASSES_ROOT\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Extensions\windows.protocol\unityhub'
if (Test-Path -LiteralPath $protocolRoot) {
    'AppModel windows.protocol\unityhub key = present'
    Get-ChildItem -LiteralPath $protocolRoot -Recurse | ForEach-Object {
        "  AppModel properties = $(@($_.Property).Count) (names and values masked)"
    }
} else {
    'No AppModel windows.protocol\unityhub key'
}

Show-Section 'Recent projects registry (up to 3 values)'
$editorKeyPath = 'Registry::HKEY_CURRENT_USER\Software\Unity Technologies\Unity Editor 5.x'
if (Test-Path -LiteralPath $editorKeyPath) {
    $key = Get-Item -LiteralPath $editorKeyPath
    $names = @($key.GetValueNames() | Where-Object { $_ -like 'RecentlyUsedProjectPaths-*' } | Select-Object -First 3)
    "recent project values = $($names.Count)"
    foreach ($name in $names) {
        $maskedName = $name -replace '_h.*$', '_h<hash>'
        "  $maskedName = <project>"
    }
} else {
    'No Unity Editor 5.x registry key'
}

Show-Section 'Editor install root'
$secondaryPath = Join-Path $hubDir 'secondaryInstallPath.json'
if (Test-Path -LiteralPath $secondaryPath) {
    $value = (Get-Content -LiteralPath $secondaryPath -Raw).Trim('"')
    if ($value) {
        'secondaryInstallPath = <install dir> (set)'
    } else {
        "secondaryInstallPath = '' (default: %ProgramFiles%\Unity\Hub\Editor)"
    }
} else {
    'No secondaryInstallPath.json'
}
