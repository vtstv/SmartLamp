#!/bin/bash
# SmartLamp Studio - User-level Installer
set -e

INSTALL_DIR="$HOME/.local/bin"
ICON_DIR="$HOME/.local/share/icons"
APP_DIR="$HOME/.local/share/applications"
DESKTOP_DIR="$HOME/Desktop"

mkdir -p "$INSTALL_DIR" "$ICON_DIR" "$APP_DIR"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Installing SmartLamp Studio..."
cp -f "$SCRIPT_DIR/SmartLampDesktop" "$INSTALL_DIR/SmartLampDesktop"
if [ -f "$SCRIPT_DIR/smartlamp" ]; then
    cp -f "$SCRIPT_DIR/smartlamp" "$INSTALL_DIR/smartlamp"
    chmod +x "$INSTALL_DIR/smartlamp"
fi
chmod +x "$INSTALL_DIR/SmartLampDesktop"

if [ -f "$SCRIPT_DIR/smartlamp.png" ]; then
    cp -f "$SCRIPT_DIR/smartlamp.png" "$ICON_DIR/smartlamp.png"
fi

sed "s|Exec=SmartLampDesktop|Exec=$INSTALL_DIR/SmartLampDesktop|g; s|Icon=smartlamp|Icon=$ICON_DIR/smartlamp.png|g" \
    "$SCRIPT_DIR/smartlamp.desktop" > "$APP_DIR/smartlamp.desktop"
chmod +x "$APP_DIR/smartlamp.desktop"

if [ -d "$DESKTOP_DIR" ]; then
    cp -f "$APP_DIR/smartlamp.desktop" "$DESKTOP_DIR/SmartLamp.desktop"
    chmod +x "$DESKTOP_DIR/SmartLamp.desktop"
fi

echo "==> Installation complete! SmartLamp Studio is available in your Applications menu and Desktop."
