#!/bin/bash
# SmartLamp Studio - User-level Uninstaller
rm -f "$HOME/.local/bin/SmartLampDesktop" "$HOME/.local/bin/smartlamp"
rm -f "$HOME/.local/share/icons/smartlamp.png"
rm -f "$HOME/.local/share/applications/smartlamp.desktop"
rm -f "$HOME/Desktop/SmartLamp.desktop"
echo "==> SmartLamp Studio successfully uninstalled."
