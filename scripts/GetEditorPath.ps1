function Get-UnityEditorsRoot {
    $userPathFile = "$env:APPDATA\UnityHub\secondaryInstallPath.json"
    if (-not (Test-Path $userPathFile)) {
        Write-Warning "Unity Hub not installed."
        return $null
    }

    # If using default path, then $userPath will be empty string
    $userPath = (Get-Content $userPathFile -Raw).Trim('"')

    if ($userPath -and (Test-Path $userPath)) {
        return $userPath
    }

    # Default path for Windows
    $defaultPath = "$env:ProgramFiles\Unity\Hub\Editor"
    if (Test-Path $defaultPath) {
        return $defaultPath
    }

    Write-Error "Could not determine Unity Editor installation path."
    return $null
}

function Get-UnityEditorPathFromVersion {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$version = $null
    )

    if (-not $version) {
        Write-Error "Cannot determine Unity version."
        return $null
    }

    # e.g. C:\Program Files\Unity\Hub\Editor
    $editorBasePath = Get-UnityEditorsRoot
    if (-not $editorBasePath) {
        Write-Error "Cannot determine Unity Editor base path."
        return $null
    }

    # Join two paths for full editor path
    $installPath = Join-Path $editorBasePath $version
    if (Test-Path $installPath) {
        return $installPath
    }

    Write-Error "Unity version $version not found in $editorBasePath"
    return $null
}

function GetUnityExecutablePath {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$version = $null
    )

    $editorPath = Get-UnityEditorPathFromVersion $version
    if (-not $editorPath) {
        return $null
    }

    # e.g. C:\Program Files\Unity\Hub\Editor\2022.3.42f1\Editor\Unity.exe
    $executablePath = Join-Path $editorPath "Editor\Unity.exe"
    if (Test-Path $executablePath) {
        return $executablePath
    }

    Write-Error "Unity executable not found at $executablePath"
    return $null
}

GetUnityExecutablePath "2022.3.42f1"

# Outputs:
# C:\Program Files\Unity\Hub\Editor\2022.3.42f1\Editor\Unity.exe
