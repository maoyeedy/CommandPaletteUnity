function Get-UnityProjectsFromJson {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$JsonPath = $null
    )

    try {
        if (-not (Test-Path -Path $JsonPath)) {
            Write-Error "File not found: $JsonPath"
            return @()
        }

        $jsonContent = Get-Content -Path $JsonPath -Raw
        $projectsData = ConvertFrom-Json $jsonContent

        if (-not $projectsData.data) {
            Write-Warning "The JSON file does not have the expected structure with 'data' property."
            return @()
        }

        # Extract project paths
        $projectPaths = $projectsData.data.PSObject.Properties | ForEach-Object {
            $_.Value.path
        }

        return $projectPaths
    }
    catch {
        Write-Error "Error parsing Unity projects file: $_"
        return @()
    }
}

# Use your own projects-v1.json
$testJsonPath = Resolve-Path -Path "$PSScriptRoot\projects-v1.json"
Get-UnityProjectsFromJson $testJsonPath

# Outputs:
# C:\Users\user\UnityProjects\_\Learn-Command-Pattern
# C:\Users\user\UnityProjects\_\Unity6-RendererFeatureExample
# C:\Users\user\UnityProjects\_\Unity6-VRS-ShadingRate
