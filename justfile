# Run `just` to list recipes. Docs: docs/DEVELOPMENT.md, docs/TROUBLESHOOTING.md.

set windows-shell := ["pwsh", "-NoLogo", "-NoProfile", "-Command"]

proj := "UnityExtension/UnityExtension.csproj"

[private]
default:
    @just --list

# Debug x64 build; `just build <dir>` builds into <dir> when the running extension locks bin/ (MSB3021)
[group('build')]
build out="":
    dotnet build {{ proj }} -c Debug -p:Platform=x64 {{ if out != "" { "-p:OutDir=" + out + "/" } else { "" } }}

# Release build with full trim/AOT warnings (IL2081/IL2104 from CsWinRT are known)
[group('build')]
trim:
    dotnet build {{ proj }} -c Release -p:Platform=x64 -p:SuppressSdkNoise=false --no-incremental

# Unsigned x64+arm64 MSIX bundle into AppPackages/, same as the CI bundle-build job
[group('build')]
bundle:
    dotnet restore {{ proj }}
    dotnet build {{ proj }} --no-restore -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always '-p:AppxBundlePlatforms="x64|arm64"' -p:AppxPackageSigningEnabled=false -p:SuppressSdkNoise=false

# Remove bin, obj and AppPackages (after identity or publish-profile changes, NETSDK1094)
[group('build')]
clean:
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue UnityExtension/bin, UnityExtension/obj, UnityExtension.Tests/bin, UnityExtension.Tests/obj, AppPackages

# ProjectParser unit tests (bootstrapper off, else the test host hangs)
[group('check')]
test:
    dotnet test UnityExtension.Tests/UnityExtension.Tests.csproj -p:Platform=x64 -p:WindowsAppSdkBootstrapInitialize=false --blame-hang --blame-hang-timeout 2m

# Formatting and all pre-commit hooks (csharpier, actionlint, gitleaks, ...)
[group('check')]
check:
    dotnet tool restore
    pre-commit run --all-files

# csproj Version/AssemblyVersion/FileVersion and manifest Identity Version must agree (X.Y.Z / X.Y.Z.0)
[group('check')]
[script('pwsh', '-NoLogo', '-NoProfile')]
versions:
    $ErrorActionPreference = 'Stop'
    $p = ([xml](Get-Content UnityExtension/UnityExtension.csproj)).Project.PropertyGroup
    $v = @($p.Version | Where-Object { $_ })[0]
    $a = @($p.AssemblyVersion | Where-Object { $_ })[0]
    $f = @($p.FileVersion | Where-Object { $_ })[0]
    $m = ([xml](Get-Content UnityExtension/Package.appxmanifest)).Package.Identity.Version
    "csproj Version=$v AssemblyVersion=$a FileVersion=$f; manifest Identity Version=$m"
    if ($a -ne "$v.0" -or $f -ne "$v.0" -or $m -ne "$v.0") { "MISMATCH: expected $v.0 for all three"; exit 1 }

# Shared packages must not exceed PowerToys' pins (the extension runs in its host)
[group('check')]
[script('pwsh', '-NoLogo', '-NoProfile')]
pins:
    $ErrorActionPreference = 'Stop'
    $ids = 'Microsoft.CommandPalette.Extensions', 'Microsoft.WindowsAppSDK', 'Microsoft.Windows.CsWinRT', 'Microsoft.Windows.SDK.BuildTools', 'Microsoft.Windows.CsWin32'
    $upstream = gh api repos/microsoft/PowerToys/contents/Directory.Packages.props --jq .content
    if ($LASTEXITCODE) { exit 1 }
    $theirs = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($upstream -join '')))
    $ours = Get-Content Directory.Packages.props -Raw
    $bad = 0
    foreach ($id in $ids) {
        $re = 'Include="' + [regex]::Escape($id) + '"\s+Version="([^"]+)"'
        $o = [regex]::Match($ours, $re).Groups[1].Value
        $t = [regex]::Match($theirs, $re).Groups[1].Value
        $over = $o -and $t -and ([version]($o -replace '-.*') -gt [version]($t -replace '-.*'))
        if ($over) { $bad++ }
        '{0,-40} ours {1,-16} PowerToys {2,-16} {3}' -f $id, $o, $t, $(if ($over) { 'ABOVE PIN' } elseif (-not $t) { '(not pinned)' } else { 'ok' })
    }
    exit $bad

# Fresh clone / new PC: audit SDKs, tools, hooks and host setup
[group('setup')]
[script('pwsh', '-NoLogo', '-NoProfile')]
doctor:
    $missing = 0
    function Check($name, $ok, $fix, [switch]$Optional) {
        if ($ok) { "OK       $name" }
        elseif ($Optional) { "OPTIONAL $name -> $fix" }
        else { "MISSING  $name -> $fix"; $script:missing++ }
    }
    function Has($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
    Check '.NET 10 SDK' ((Has dotnet) -and (dotnet --list-sdks | Select-String '^10\.')) 'winget install Microsoft.DotNet.SDK.10'
    Check 'dotnet local tools' ((Has dotnet) -and ((dotnet tool restore *> $null) -or $LASTEXITCODE -eq 0)) 'dotnet tool restore'
    Check 'pre-commit' (Has pre-commit) 'uv tool install pre-commit'
    Check 'git hooks installed' ((Test-Path .git/hooks/pre-commit) -and (Test-Path .git/hooks/pre-push)) 'pre-commit install'
    $dev = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
    Check 'Developer Mode (debug register)' ($dev -eq 1) 'Settings > System > For developers > Developer Mode'
    Check 'PowerToys' ((Test-Path "$env:LOCALAPPDATA\PowerToys\PowerToys.exe") -or (Test-Path "$env:ProgramFiles\PowerToys\PowerToys.exe")) 'winget install Microsoft.PowerToys'
    Check 'gh (just pins, releases)' (Has gh) 'winget install GitHub.cli' -Optional
    Check 'winapp (dev registration, UI inspect)' (Has winapp) 'winget install Microsoft.WinAppCli' -Optional
    Check 'actionlint' (Has actionlint) 'winget install rhysd.actionlint' -Optional
    Check 'msstore (Store submissions)' (Has msstore) 'winget install "Microsoft Store Developer CLI"' -Optional
    Check 'ctx7 (library docs)' (Has ctx7) 'bun i -g ctx7' -Optional
    Check 'Unity Hub projects-v1.json' (Test-Path "$env:APPDATA\UnityHub\projects-v1.json") 'install Unity Hub and add a project' -Optional
    if ($missing) { "$missing required item(s) missing"; exit 1 }

# Debug build + dev registration into Command Palette; restart PowerToys afterwards
[group('deploy')]
[script('pwsh', '-NoLogo', '-NoProfile')]
register:
    $ErrorActionPreference = 'Stop'
    $release = Get-AppxPackage YidiMao.CommandPalette-Unity
    if ($release) {
        "A release package ($($release.Version)) is installed and blocks -Register (0x80073CFB). Remove it first:"
        "  Get-AppxPackage YidiMao.CommandPalette-Unity | Remove-AppxPackage"
        exit 1
    }
    dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64 -p:WindowsPackageType=MSIX
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    $manifest = Get-ChildItem UnityExtension/bin/x64/Debug -Recurse -Filter AppxManifest.xml | Where-Object { $_.Directory.Name -eq 'win-x64' } | Select-Object -First 1
    if (-not $manifest) { 'No AppxManifest.xml under UnityExtension/bin/x64/Debug/*/win-x64'; exit 1 }
    $assets = New-Item -ItemType Directory -Force (Join-Path $manifest.DirectoryName 'Assets')
    Copy-Item -Recurse -Force UnityExtension/Assets/* $assets
    Add-AppxPackage -Register $manifest.FullName -ForceUpdateFromAnyVersion
    "Registered $($manifest.FullName)"
