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

        # Extract project details as custom objects
        $projects = $projectsData.data.PSObject.Properties | ForEach-Object {
            $projectInfo = $_.Value
            [PSCustomObject]@{
                Title      = $projectInfo.title
                Path       = $projectInfo.path
                Version    = $projectInfo.version
                IsFavorite = $projectInfo.isFavorite
            }
        }
        return $projects
    }
    catch {
        Write-Error "Error parsing Unity projects file: $_"
        return @()
    }
}

# Use your own projects-v1.json
$testJsonPath = Resolve-Path -Path "$PSScriptRoot\projects-v1.json"
Get-UnityProjectsFromJson $testJsonPath
# Get-UnityProjectsFromJson $testJsonPath | Format-Table -Property Title, Version
# Outputs:
# Title                          Path                                                   Version        IsFavorite
# -----                          ----                                                   -------        ----------
# Learn-Command-Pattern          C:\Users\jerkl\UnityProjects\_\Learn-Command-Pattern  6000.0.42f1    True
# Unity6-RendererFeatureExample  C:\Users\jerkl\UnityProjects\_\Unity6-RendererFeat... 2022.3.51f1    False
# Unity6-VRS-ShadingRate         C:\Users\jerkl\UnityProjects\_\Unity6-VRS-ShadingRate 6000.1.0f1     False
