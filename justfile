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

# Formatting and every pre-commit check (csharpier, actionlint, gitleaks, ...); pre-push checks
# (dotnet format analyzers, vulnerability scan) only run on `git push`.
[group('check')]
check:
    dotnet tool restore
    pwsh -NoLogo -NoProfile -File .githooks/pre-commit.ps1 --all-files

# Windows dev-only: editors found in the same registry keys EditorParser reads
[group('check')]
editors:
    pwsh -NoLogo -NoProfile -File ./scripts/Get-UnityEditors.ps1
    exit $LASTEXITCODE

# Windows dev-only: local Hub facts used by docs/reference/unity (read-only)
[group('check')]
hubfacts:
    pwsh -NoLogo -NoProfile -File ./scripts/Get-HubFacts.ps1
    exit $LASTEXITCODE

# Windows dev-only: check relative Markdown links and heading anchors
[group('check')]
docs-check:
    pwsh -NoLogo -NoProfile -File ./scripts/Check-DocLinks.ps1
    exit $LASTEXITCODE

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

# Runtime-shared packages must not exceed the latest PowerToys release (build-only SDK.BuildTools/CsWin32 may)
[group('check')]
[script('pwsh', '-NoLogo', '-NoProfile')]
pins:
    ./.github/scripts/CheckPins.ps1
    exit $LASTEXITCODE

# Point git at the committed hooks in .githooks/ (once per clone)
[group('setup')]
hooks:
    git config core.hooksPath .githooks

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
    Check '.NET SDK 10 or newer' ((Has dotnet) -and (dotnet --list-sdks | Where-Object { [int]($_ -split '[.]')[0] -ge 10 })) 'winget install Microsoft.DotNet.SDK.10'
    Check 'dotnet local tools' ((Has dotnet) -and ((dotnet tool restore *> $null) -or $LASTEXITCODE -eq 0)) 'dotnet tool restore'
    $pinsText = Get-Content .githooks/pins.ps1 -Raw
    $actionlintPin = [regex]::Match($pinsText, "ActionlintVersion\s*=\s*'([^']+)'").Groups[1].Value
    $gitleaksPin = [regex]::Match($pinsText, "GitleaksVersion\s*=\s*'([^']+)'").Groups[1].Value
    . ./.githooks/pins.ps1
    $actionlintVersion = if (Has actionlint) { [regex]::Match("$(actionlint --version 2>$null | Select-Object -First 1)", '\d+\.\d+\.\d+').Value } else { '' }
    $gitleaksVersion = if (Has gitleaks) { "$(gitleaks version 2>$null)".Trim() } else { '' }
    Check "actionlint $actionlintPin (installed $(if ($actionlintVersion) { $actionlintVersion } else { 'none' }))" ((Has actionlint) -and (Test-ToolAtLeast $actionlintVersion $actionlintPin)) 'winget install rhysd.actionlint'
    Check "gitleaks $gitleaksPin (installed $(if ($gitleaksVersion) { $gitleaksVersion } else { 'none' }))" ((Has gitleaks) -and (Test-ToolAtLeast $gitleaksVersion $gitleaksPin)) 'winget install gitleaks.gitleaks'
    Check 'git hooks path (core.hooksPath .githooks)' ("$(git config core.hooksPath 2>$null)".Trim() -eq '.githooks') 'just hooks'
    $hookModes = @('.githooks/pre-commit', '.githooks/pre-push' | ForEach-Object { "$(git ls-files -s $_)".Trim() })
    Check 'git hooks committed executable (100755)' (@($hookModes | Where-Object { $_ -like '100755 *' }).Count -eq 2) 'git update-index --chmod=+x .githooks/pre-commit .githooks/pre-push'
    $dev = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
    Check 'Developer Mode (debug register)' ($dev -eq 1) 'Settings > System > For developers > Developer Mode'
    Check 'PowerToys' ((Test-Path "$env:LOCALAPPDATA\PowerToys\PowerToys.exe") -or (Test-Path "$env:ProgramFiles\PowerToys\PowerToys.exe")) 'winget install Microsoft.PowerToys'
    Check 'gh (just pins, releases)' (Has gh) 'winget install GitHub.cli' -Optional
    Check 'winapp (dev registration, UI inspect)' (Has winapp) 'winget install Microsoft.WinAppCli' -Optional
    Check 'msstore (Store submissions)' (Has msstore) 'winget install "Microsoft Store Developer CLI"' -Optional
    Check 'ctx7 (library docs)' (Has ctx7) 'bun i -g ctx7' -Optional
    Check 'Unity Hub projects-v1.json' (Test-Path "$env:APPDATA\UnityHub\projects-v1.json") 'install Unity Hub and add a project' -Optional
    if ($missing) { "$missing required item(s) missing"; exit 1 }

# Debug build + dev registration into Command Palette, then reload its extensions
[group('deploy')]
[script('pwsh', '-NoLogo', '-NoProfile')]
register:
    $ErrorActionPreference = 'Stop'
    # Uninstall first: a release install blocks -Register (0x80073CFB), and a running dev
    # registration locks bin/ (MSB3021). Removal also stops the extension, so Command Palette can
    # stay open. Dev data (settings) is kept; a release install's settings are not.
    foreach ($installed in @(Get-AppxPackage YidiMao.CommandPalette-Unity)) {
        if ($installed.IsDevelopmentMode) {
            $kind = 'dev registration'
            $remove = @{ Package = $installed.PackageFullName; PreserveApplicationData = $true }
        } else {
            $kind = 'release package'
            $remove = @{ Package = $installed.PackageFullName }
        }
        "Removing $kind $($installed.Version)"
        try {
            Remove-AppxPackage @remove
            "Removed $kind $($installed.Version)"
        } catch {
            # A concurrent register/uninstall can make the package disappear between
            # Get-AppxPackage and Remove-AppxPackage (0x80073CF1). That is fine; any
            # other removal failure still stops the recipe.
            if ($_.Exception.Message -match '0x80073CF1|Package was not found') {
                "$kind $($installed.Version) is already gone; continuing"
            } else {
                throw
            }
        }
    }
    dotnet build UnityExtension/UnityExtension.csproj -c Debug -p:Platform=x64 -p:WindowsPackageType=MSIX
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    $manifest = Get-ChildItem UnityExtension/bin/x64/Debug -Recurse -Filter AppxManifest.xml | Where-Object { $_.Directory.Name -eq 'win-x64' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $manifest) { 'No AppxManifest.xml under UnityExtension/bin/x64/Debug/*/win-x64'; exit 1 }
    $assets = New-Item -ItemType Directory -Force (Join-Path $manifest.DirectoryName 'Assets')
    Copy-Item -Recurse -Force UnityExtension/Assets/* $assets
    Add-AppxPackage -Register $manifest.FullName -ForceUpdateFromAnyVersion
    "Registered $($manifest.FullName)."
    # A remove/re-register leaves the old provider in CmdPal's list until it reloads, so a
    # second register can look like two extensions. Let the old extension process exit and
    # CmdPal's package-catalog events settle first; reloading too early still sees two
    # entries. This needs Command Palette running and "Enable external command links" on.
    if (Get-Process Microsoft.CmdPal.UI -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 3
        Start-Process x-cmdpal://reload
        "Sent x-cmdpal://reload; confirm in Command Palette if it asks."
    } else {
        "Command Palette is not running; start it to load the extension."
    }
