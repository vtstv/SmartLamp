# ============================================================================
# SmartLamp Studio - Windows PowerShell Linux Publish Script (Ubuntu & CachyOS)
# ============================================================================
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$distDir = Join-Path $rootDir "dist\linux-release"
$appDir = Join-Path $rootDir "src\SmartLamp.Desktop"

Write-Host "==> Compiling SmartLamp Desktop for linux-x64..." -ForegroundColor Cyan
dotnet publish "$appDir\SmartLamp.Desktop.csproj" `
    -c Release `
    -r linux-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$distDir\SmartLamp"

Write-Host "==> Adding assets and installer scripts..." -ForegroundColor Cyan
Copy-Item "$appDir\Assets\app_icon.png" "$distDir\SmartLamp\smartlamp.png" -Force

$desktopContent = @"
[Desktop Entry]
Type=Application
Name=SmartLamp Studio
Comment=Control Tuya Smart Lamps Locally with Sub-35ms Latency
Exec=SmartLampDesktop
Icon=smartlamp
Terminal=false
Categories=Utility;HardwareSettings;
Keywords=tuya;smartlamp;light;lamp;
StartupNotify=true
"@
Set-Content -Path "$distDir\SmartLamp\smartlamp.desktop" -Value $desktopContent -NoNewline

$installScript = @"
#!/bin/bash
set -e
INSTALL_DIR="`$HOME/.local/bin"
ICON_DIR="`$HOME/.local/share/icons"
APP_DIR="`$HOME/.local/share/applications"
DESKTOP_DIR="`$HOME/Desktop"

mkdir -p "`$INSTALL_DIR" "`$ICON_DIR" "`$APP_DIR" "`$DESKTOP_DIR"

SCRIPT_DIR="`$(cd "`$(dirname "`$0")" && pwd)"

echo "==> Installing SmartLamp Studio..."
cp -f "`$SCRIPT_DIR/SmartLampDesktop" "`$INSTALL_DIR/SmartLampDesktop"
chmod +x "`$INSTALL_DIR/SmartLampDesktop"

cp -f "`$SCRIPT_DIR/smartlamp.png" "`$ICON_DIR/smartlamp.png"

sed "s|Exec=SmartLampDesktop|Exec=`$INSTALL_DIR/SmartLampDesktop|g; s|Icon=smartlamp|Icon=`$ICON_DIR/smartlamp.png|g" \
    "`$SCRIPT_DIR/smartlamp.desktop" > "`$APP_DIR/smartlamp.desktop"
chmod +x "`$APP_DIR/smartlamp.desktop"

if [ -d "`$DESKTOP_DIR" ]; then
    cp -f "`$APP_DIR/smartlamp.desktop" "`$DESKTOP_DIR/SmartLamp.desktop"
    chmod +x "`$DESKTOP_DIR/SmartLamp.desktop"
fi

echo "==> Installation complete! SmartLamp Studio is ready in your Application Menu & Desktop."
"@
[System.IO.File]::WriteAllText("$distDir\SmartLamp\install.sh", $installScript.Replace("`r`n", "`n"))

$uninstallScript = @"
#!/bin/bash
rm -f "`$HOME/.local/bin/SmartLampDesktop"
rm -f "`$HOME/.local/share/icons/smartlamp.png"
rm -f "`$HOME/.local/share/applications/smartlamp.desktop"
rm -f "`$HOME/Desktop/SmartLamp.desktop"
echo "==> SmartLamp Studio uninstalled."
"@
[System.IO.File]::WriteAllText("$distDir\SmartLamp\uninstall.sh", $uninstallScript.Replace("`r`n", "`n"))

Write-Host "==> Creating release tarball..." -ForegroundColor Cyan
tar -czvf "$rootDir\dist\SmartLampStudio-linux-x64.tar.gz" -C "$distDir" SmartLamp

Write-Host "==> Linux package successfully created at: dist\SmartLampStudio-linux-x64.tar.gz" -ForegroundColor Green
