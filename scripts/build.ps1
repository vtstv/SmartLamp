<#
.SYNOPSIS
    SmartLamp Studio - Unified Multi-Platform Build & Packaging Script.
.DESCRIPTION
    Builds and packages SmartLamp Studio projects for Windows, Linux, and CLI.
.PARAMETER Target
    Build target: 'All', 'Windows', 'Linux', 'Desktop', 'Cli', or 'Library'. Default is 'All'.
.PARAMETER Configuration
    Build configuration: 'Release' or 'Debug'. Default is 'Release'.
.PARAMETER Arch
    Target architecture: 'x64', 'arm64', or 'all'. Default is 'x64'.
.PARAMETER Pack
    Creates distribution packages (zip, tar.gz, nupkg) in the releases/ directory.
.PARAMETER Clean
    Performs a clean build by removing previous build artifacts first.
.EXAMPLE
    .\scripts\build.ps1
    .\scripts\build.ps1 -Target Windows -Pack
    .\scripts\build.ps1 -Target Linux -Arch all -Pack
    .\scripts\build.ps1 -Clean -Target All -Pack
#>
[CmdletBinding()]
param(
    [ValidateSet("All", "Windows", "Linux", "Desktop", "Cli", "Library")]
    [string]$Target = "All",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "arm64", "all")]
    [string]$Arch = "x64",

    [switch]$Pack,

    [switch]$Clean,

    [switch]$NoTest
)

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $rootDir "dist"
$releasesDir = Join-Path $rootDir "releases"
$packagingDir = Join-Path $rootDir "packaging"

# 1. Stop any running instances to avoid file locks
Get-Process | Where-Object { $_.ProcessName -like "SmartLamp*" -or $_.ProcessName -eq "smartlamp" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# 2. Clean if requested
if ($Clean) {
    & (Join-Path $PSScriptRoot "clean.ps1")
}

# 2. Extract Version from root VERSION file
$versionFile = Join-Path $rootDir "VERSION"
if (Test-Path $versionFile) {
    $appVersion = (Get-Content $versionFile -Raw).Trim()
} else {
    $appVersion = "3.2.0"
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "      SmartLamp Studio Unified Build System v$appVersion" -ForegroundColor Cyan
Write-Host "      Target: $Target | Config: $Configuration | Arch: $Arch | Pack: $Pack" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
if ($Pack) {
    New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null
}

# Resolve Linux Runtime Identifiers
$linuxRids = @()
switch ($Arch) {
    "x64"   { $linuxRids = @("linux-x64") }
    "arm64" { $linuxRids = @("linux-arm64") }
    "all"   { $linuxRids = @("linux-x64", "linux-arm64") }
}

# ------------------------------------------------------------------
# Pre-Build Verification: Run Automated Unit Tests
# ------------------------------------------------------------------
if (-not $NoTest) {
    Write-Host "`n[0] Running Automated Unit Tests..." -ForegroundColor Yellow
    $projTests = Join-Path $rootDir "tests\SmartLamp.Tests\SmartLamp.Tests.csproj"
    if (Test-Path $projTests) {
        dotnet test $projTests -c $Configuration --verbosity quiet --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Automated unit tests failed! Aborting build to ensure release stability."
            exit $LASTEXITCODE
        }
        Write-Host "  --> All unit tests passed successfully!" -ForegroundColor Green
    }
}

# ------------------------------------------------------------------
# Target: Library (TuyaNet.Native)
# ------------------------------------------------------------------
if ($Target -in @("All", "Library", "Windows")) {
    Write-Host "`n[1] Building TuyaNet.Native Protocol Library..." -ForegroundColor Yellow
    $projLib = Join-Path $rootDir "libs\TuyaNet.Native\TuyaNet.Native.csproj"
    dotnet build $projLib -c $Configuration

    if ($Pack) {
        Write-Host "  --> Packing TuyaNet.Native NuGet package..." -ForegroundColor Green
        dotnet pack $projLib -c $Configuration -o $releasesDir
    }
}

# ------------------------------------------------------------------
# Target: Windows Desktop (SmartLampApp WPF)
# ------------------------------------------------------------------
if ($Target -in @("All", "Windows")) {
    Write-Host "`n[2] Compiling Windows WPF App (win-x64)..." -ForegroundColor Yellow
    $projWpf = Join-Path $rootDir "src\SmartLampApp.csproj"
    dotnet publish $projWpf -c $Configuration -r win-x64 --no-self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $distDir

    $iconSrc = Join-Path $rootDir "src\Assets\app_icon.ico"
    if (Test-Path $iconSrc) {
        Copy-Item -Path $iconSrc -Destination (Join-Path $distDir "app_icon.ico") -Force
    }

    if ($Pack) {
        Write-Host "  --> Creating Windows Release Packages..." -ForegroundColor Green
        $zipPath = Join-Path $releasesDir "SmartLampStudio_v${appVersion}_win-x64.zip"
        $readmePath = Join-Path $rootDir "README.md"
        $licensePath = Join-Path $rootDir "LICENSE"
        $exePath = Join-Path $distDir "SmartLampApp.exe"

        Compress-Archive -Path $exePath, $readmePath, $licensePath -DestinationPath $zipPath -Force
        Copy-Item -Path $exePath -Destination (Join-Path $releasesDir "SmartLampApp_v${appVersion}.exe") -Force
        Write-Host "  --> Created: $zipPath" -ForegroundColor DarkCyan

        # Build Inno Setup Installer (.exe)
        $isccPath = $null
        $possiblePaths = @(
            "iscc",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe"
        )
        foreach ($p in $possiblePaths) {
            if ($p -eq "iscc" -and (Get-Command iscc -ErrorAction SilentlyContinue)) {
                $isccPath = "iscc"
                break
            }
            elseif (Test-Path $p) {
                $isccPath = $p
                break
            }
        }

        if ($isccPath) {
            Write-Host "  --> Building Windows Inno Setup Installer..." -ForegroundColor Green
            $issFile = Join-Path $packagingDir "windows\setup.iss"
            & $isccPath "/DAppVersion=$appVersion" $issFile
            Write-Host "  --> Created Inno Setup Installer in releases/" -ForegroundColor DarkCyan
        }
        else {
            Write-Host "  [!] Inno Setup (iscc.exe) not found. Skipping installer. (Install with: winget install JRSoftware.InnoSetup)" -ForegroundColor Yellow
        }
    }
}

# ------------------------------------------------------------------
# Target: Cross-Platform Native CLI (SmartLamp.Cli)
# ------------------------------------------------------------------
if ($Target -in @("All", "Linux", "Cli")) {
    Write-Host "`n[3] Compiling Native CLI..." -ForegroundColor Yellow
    $projCli = Join-Path $rootDir "src\SmartLamp.Cli\SmartLamp.Cli.csproj"

    foreach ($rid in $linuxRids) {
        Write-Host "  --> Publishing CLI for $rid..." -ForegroundColor Yellow
        $outCli = Join-Path $distDir "cli-$rid"
        dotnet publish $projCli -c $Configuration -r $rid --self-contained `
            -p:PublishSingleFile=true -p:PublishTrimmed=true -o $outCli

        if ($Pack) {
            $cliArchive = Join-Path $releasesDir "SmartLamp_CLI_v${appVersion}_${rid}.tar.gz"
            tar -czf $cliArchive -C $outCli smartlamp
            Write-Host "  --> Created: $cliArchive" -ForegroundColor DarkCyan
        }
    }
}

# ------------------------------------------------------------------
# Target: Cross-Platform Avalonia Desktop (SmartLamp.Desktop)
# ------------------------------------------------------------------
if ($Target -in @("All", "Linux", "Desktop")) {
    Write-Host "`n[4] Compiling Avalonia Desktop (Linux)..." -ForegroundColor Yellow
    $projDesktop = Join-Path $rootDir "src\SmartLamp.Desktop\SmartLamp.Desktop.csproj"

    foreach ($rid in $linuxRids) {
        Write-Host "  --> Publishing Avalonia Desktop for $rid..." -ForegroundColor Yellow
        $outDesktop = Join-Path $distDir "desktop-$rid"
        dotnet publish $projDesktop -c $Configuration -r $rid --self-contained `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outDesktop

        if ($Pack) {
            Write-Host "  --> Packaging Standalone Tarball for $rid..." -ForegroundColor Green
            $bundleDir = Join-Path $distDir "bundle-$rid\SmartLampStudio"
            New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

            Copy-Item -Path (Join-Path $outDesktop "SmartLampDesktop") -Destination $bundleDir -Force
            $cliBin = Join-Path $distDir "cli-$rid\smartlamp"
            if (Test-Path $cliBin) {
                Copy-Item -Path $cliBin -Destination $bundleDir -Force
            }
            Copy-Item -Path (Join-Path $rootDir "src\SmartLamp.Desktop\Assets\app_icon.png") -Destination (Join-Path $bundleDir "smartlamp.png") -Force
            Copy-Item -Path (Join-Path $packagingDir "desktop\smartlamp.desktop") -Destination $bundleDir -Force
            Copy-Item -Path (Join-Path $packagingDir "scripts\install.sh") -Destination $bundleDir -Force
            Copy-Item -Path (Join-Path $packagingDir "scripts\uninstall.sh") -Destination $bundleDir -Force

            $tarPath = Join-Path $releasesDir "SmartLampStudio_v${appVersion}_${rid}.tar.gz"
            tar -czf $tarPath -C (Join-Path $distDir "bundle-$rid") SmartLampStudio
            Write-Host "  --> Created: $tarPath" -ForegroundColor DarkCyan
        }
    }
}

Write-Host "`n================================================================" -ForegroundColor Green
Write-Host "      BUILD COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
if ($Pack) {
    Write-Host "Packages in: $releasesDir" -ForegroundColor White
    Get-ChildItem -Path $releasesDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
}
