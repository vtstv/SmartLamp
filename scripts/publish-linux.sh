#!/bin/bash
# ============================================================================
# SmartLamp Studio - Linux Automated Build & Package Script (Ubuntu & CachyOS)
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
DIST_DIR="$ROOT_DIR/dist/linux-release"
APP_DIR="$ROOT_DIR/src/SmartLamp.Desktop"

echo "==> Building SmartLamp Desktop for linux-x64..."
dotnet publish "$APP_DIR/SmartLamp.Desktop.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$DIST_DIR/SmartLamp"

echo "==> Adding assets and installer scripts..."
cp "$APP_DIR/Assets/app_icon.png" "$DIST_DIR/SmartLamp/smartlamp.png"

cat << 'EOF' > "$DIST_DIR/SmartLamp/smartlamp.desktop"
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
EOF

cat << 'EOF' > "$DIST_DIR/SmartLamp/install.sh"
#!/bin/bash
set -e
INSTALL_DIR="$HOME/.local/bin"
ICON_DIR="$HOME/.local/share/icons"
APP_DIR="$HOME/.local/share/applications"
DESKTOP_DIR="$HOME/Desktop"

mkdir -p "$INSTALL_DIR" "$ICON_DIR" "$APP_DIR" "$DESKTOP_DIR"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Installing SmartLamp Studio..."
cp -f "$SCRIPT_DIR/SmartLampDesktop" "$INSTALL_DIR/SmartLampDesktop"
chmod +x "$INSTALL_DIR/SmartLampDesktop"

cp -f "$SCRIPT_DIR/smartlamp.png" "$ICON_DIR/smartlamp.png"

sed "s|Exec=SmartLampDesktop|Exec=$INSTALL_DIR/SmartLampDesktop|g; s|Icon=smartlamp|Icon=$ICON_DIR/smartlamp.png|g" \
    "$SCRIPT_DIR/smartlamp.desktop" > "$APP_DIR/smartlamp.desktop"
chmod +x "$APP_DIR/smartlamp.desktop"

if [ -d "$DESKTOP_DIR" ]; then
    cp -f "$APP_DIR/smartlamp.desktop" "$DESKTOP_DIR/SmartLamp.desktop"
    chmod +x "$DESKTOP_DIR/SmartLamp.desktop"
fi

echo "==> Installation complete! SmartLamp Studio is ready in your Application Menu & Desktop."
EOF
chmod +x "$DIST_DIR/SmartLamp/install.sh"

cat << 'EOF' > "$DIST_DIR/SmartLamp/uninstall.sh"
#!/bin/bash
rm -f "$HOME/.local/bin/SmartLampDesktop"
rm -f "$HOME/.local/share/icons/smartlamp.png"
rm -f "$HOME/.local/share/applications/smartlamp.desktop"
rm -f "$HOME/Desktop/SmartLamp.desktop"
echo "==> SmartLamp Studio uninstalled."
EOF
chmod +x "$DIST_DIR/SmartLamp/uninstall.sh"

echo "==> Creating release tarball..."
tar -czvf "$ROOT_DIR/dist/SmartLampStudio-linux-x64.tar.gz" -C "$DIST_DIR" SmartLamp

echo "==> Linux package successfully created at: dist/SmartLampStudio-linux-x64.tar.gz"
