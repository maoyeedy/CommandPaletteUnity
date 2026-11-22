# Pinned versions of the hook tools that are not installed from the repo (actionlint, gitleaks).
# Bump here, then `just doctor` checks the installed binaries against these pins.

$script:ActionlintVersion = 'v1.7.12'
$script:GitleaksVersion = 'v8.30.1'

# Numeric compare so '1.17.1' passes a 'v1.7.12' pin; missing parts count as 0.
function Test-ToolAtLeast([string]$Installed, [string]$Pin) {
    $a = @($Installed.TrimStart('v') -split '[^0-9]+' | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ })
    $b = @($Pin.TrimStart('v') -split '[^0-9]+' | Where-Object { $_ -ne '' } | ForEach-Object { [int]$_ })
    for ($i = 0; $i -lt [Math]::Max($a.Count, $b.Count); $i++) {
        $x = if ($i -lt $a.Count) { $a[$i] } else { 0 }
        $y = if ($i -lt $b.Count) { $b[$i] } else { 0 }
        if ($x -ne $y) { return $x -gt $y }
    }
    return $true
}
