# Dev-only, read-only: print the local Hub facts used in docs/reference/unity.
# Does not start Unity Hub, run the Unity CLI, or modify the registry.
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
    "executablePath= $($info.executablePath)"
} else {
    "No $infoPath"
}

Show-Section 'Running Hub process'
$hubProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity Hub.exe'" -ErrorAction SilentlyContinue)
if ($hubProcesses) {
    $hubProcesses | Select-Object ProcessId, ExecutablePath, CommandLine | Format-List
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
    Get-ChildItem -LiteralPath $protocolRoot -Recurse | ForEach-Object {
        "AppModel key = $($_.Name)"
        foreach ($name in $_.Property) { "  $name = $($_.GetValue($name))" }
    }
} else {
    'No AppModel windows.protocol\unityhub key'
}

Show-Section 'Recent projects registry (up to 3 values)'
$editorKeyPath = 'Registry::HKEY_CURRENT_USER\Software\Unity Technologies\Unity Editor 5.x'
if (Test-Path -LiteralPath $editorKeyPath) {
    $key = Get-Item -LiteralPath $editorKeyPath
    $names = @($key.GetValueNames() | Where-Object { $_ -like 'RecentlyUsedProjectPaths-*' } | Select-Object -First 3)
    foreach ($name in $names) {
        $bytes = $key.GetValue($name)
        $path = if ($bytes -is [byte[]]) {
            ([Text.Encoding]::UTF8.GetString($bytes) -split "`0" | Where-Object { $_ } | Select-Object -First 1)
        } else {
            "$($bytes.GetType().Name) value"
        }
        "$name = $path"
    }
} else {
    'No Unity Editor 5.x registry key'
}

Show-Section 'Editor install root'
$secondaryPath = Join-Path $hubDir 'secondaryInstallPath.json'
if (Test-Path -LiteralPath $secondaryPath) {
    $value = (Get-Content -LiteralPath $secondaryPath -Raw).Trim('"')
    if ($value) {
        "secondaryInstallPath = $value"
    } else {
        "secondaryInstallPath = '' (default: %ProgramFiles%\Unity\Hub\Editor)"
    }
} else {
    'No secondaryInstallPath.json'
}
