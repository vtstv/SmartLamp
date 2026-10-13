<#
.SYNOPSIS
    Deep clean script for SmartLamp Studio.
.DESCRIPTION
    Removes all build artifacts, temporary files, logs, and output directories.
.EXAMPLE
    .\scripts\clean.ps1
    .\scripts\clean.ps1 -Full
#>
[CmdletBinding()]
param(
    [switch]$Full  # Also wipes releases/ directory
)

$ErrorActionPreference = "Continue"

$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host "==> Cleaning SmartLamp Studio build artifacts..." -ForegroundColor Yellow

# 1. Clean bin and obj folders across repository
$binObjFolders = Get-ChildItem -Path $rootDir -Include "bin", "obj" -Recurse -Directory -Force |
    Where-Object { $_.FullName -notmatch "\\\.git\\" }

foreach ($folder in $binObjFolders) {
    Write-Host "  Removing $($folder.FullName.Replace($rootDir, ''))" -ForegroundColor DarkGray
    Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. Clean dist directory
$distDir = Join-Path $rootDir "dist"
if (Test-Path $distDir) {
    Write-Host "  Cleaning dist/..." -ForegroundColor DarkGray
    Remove-Item -Path "$distDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Clean stray logs and temporary files
$tempFiles = Get-ChildItem -Path $rootDir -Include "*.log", "*.tmp" -Recurse -File -Force |
    Where-Object { $_.FullName -notmatch "\\\.git\\" }

foreach ($file in $tempFiles) {
    Write-Host "  Removing $($file.FullName.Replace($rootDir, ''))" -ForegroundColor DarkGray
    Remove-Item -Path $file.FullName -Force -ErrorAction SilentlyContinue
}

# 4. Clean releases if -Full specified
if ($Full) {
    $releasesDir = Join-Path $rootDir "releases"
    if (Test-Path $releasesDir) {
        Write-Host "  Cleaning releases/ (-Full specified)..." -ForegroundColor DarkGray
        Remove-Item -Path "$releasesDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "==> Workspace cleaned successfully." -ForegroundColor Green
