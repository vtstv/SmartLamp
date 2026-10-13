#!/bin/bash
# ============================================================================
# SmartLamp Studio - Multi-Distribution Linux Packaging Script
# Targets: Ubuntu/Debian (.deb), CachyOS/Arch (.pkg.tar.zst), Tarball (.tar.gz)
# Architectures: x86_64 (amd64) and aarch64 (arm64)
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT_DIR/releases"
SRC_APP="$ROOT_DIR/src/SmartLamp.Desktop"
SRC_CLI="$ROOT_DIR/src/SmartLamp.Cli"

# Detect version
VERSION="3.1.0"
if [ -f "$ROOT_DIR/src/AppVersion.cs" ]; then
    VERSION=$(grep 'public const string Version' "$ROOT_DIR/src/AppVersion.cs" | head -n 1 | sed -E 's/.*"([^"]+)".*/\1/')
fi
echo "==> Packaging SmartLamp Studio v$VERSION for Linux (x64 and arm64)..."

mkdir -p "$OUT_DIR"
BUILD_DIR="$ROOT_DIR/dist/linux-build"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Check packaging utilities
if ! command -v zstd >/dev/null 2>&1; then
    echo "Installing zstd..."
    if command -v apt-get >/dev/null 2>&1; then
        sudo apt-get update && sudo apt-get install -y zstd
    elif command -v pacman >/dev/null 2>&1; then
        sudo pacman -S --noconfirm zstd
    fi
fi

ARCHS=("linux-x64:amd64:x86_64" "linux-arm64:arm64:aarch64")

for entry in "${ARCHS[@]}"; do
    IFS=":" read -r RID DEB_ARCH ARCH_ARCH <<< "$entry"
    echo "=================================================================="
    echo "==> Processing Architecture: RID=$RID | DEB=$DEB_ARCH | ARCH=$ARCH_ARCH"
    echo "=================================================================="

    DESKTOP_DIR="$BUILD_DIR/$RID/desktop"
    CLI_DIR="$BUILD_DIR/$RID/cli"
    mkdir -p "$DESKTOP_DIR" "$CLI_DIR"

    # 1. Compile Desktop App
    echo "--> Compiling Avalonia Desktop for $RID..."
    dotnet publish "$SRC_APP/SmartLamp.Desktop.csproj" \
        -c Release \
        -r "$RID" \
        --self-contained \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$DESKTOP_DIR"

    # 2. Compile CLI App
    echo "--> Compiling Native CLI for $RID..."
    dotnet publish "$SRC_CLI/SmartLamp.Cli.csproj" \
        -c Release \
        -r "$RID" \
        --self-contained \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -o "$CLI_DIR"

    # 3. Create Universal Tarball (.tar.gz) with installer
    echo "--> Building Standalone Tarball for $RID..."
    BUNDLE_DIR="$BUILD_DIR/$RID/SmartLampStudio"
    mkdir -p "$BUNDLE_DIR"
    cp -f "$DESKTOP_DIR/SmartLampDesktop" "$BUNDLE_DIR/"
    cp -f "$CLI_DIR/smartlamp" "$BUNDLE_DIR/"
    cp -f "$SRC_APP/Assets/app_icon.png" "$BUNDLE_DIR/smartlamp.png"
    
    # Desktop shortcut template
    cat << 'EOF' > "$BUNDLE_DIR/smartlamp.desktop"
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

    # One-click installer
    cat << 'EOF' > "$BUNDLE_DIR/install.sh"
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
cp -f "$SCRIPT_DIR/smartlamp" "$INSTALL_DIR/smartlamp"
chmod +x "$INSTALL_DIR/SmartLampDesktop" "$INSTALL_DIR/smartlamp"

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
    chmod +x "$BUNDLE_DIR/install.sh"

    # Uninstaller
    cat << 'EOF' > "$BUNDLE_DIR/uninstall.sh"
#!/bin/bash
rm -f "$HOME/.local/bin/SmartLampDesktop" "$HOME/.local/bin/smartlamp"
rm -f "$HOME/.local/share/icons/smartlamp.png"
rm -f "$HOME/.local/share/applications/smartlamp.desktop"
rm -f "$HOME/Desktop/SmartLamp.desktop"
echo "==> SmartLamp Studio uninstalled."
EOF
    chmod +x "$BUNDLE_DIR/uninstall.sh"

    tar -czf "$OUT_DIR/SmartLampStudio_v${VERSION}_${RID}.tar.gz" -C "$BUILD_DIR/$RID" SmartLampStudio
    tar -czf "$OUT_DIR/SmartLamp_CLI_v${VERSION}_${RID}.tar.gz" -C "$CLI_DIR" smartlamp

    # 4. Create Debian / Ubuntu Package (.deb)
    if command -v dpkg-deb >/dev/null 2>&1; then
        echo "--> Building Debian/Ubuntu package (.deb) for $DEB_ARCH..."
        DEB_ROOT="$BUILD_DIR/$RID/deb-root"
        mkdir -p "$DEB_ROOT/DEBIAN" "$DEB_ROOT/usr/bin" "$DEB_ROOT/usr/share/applications" \
                 "$DEB_ROOT/usr/share/icons/hicolor/512x512/apps" "$DEB_ROOT/usr/share/pixmaps"

        cp -f "$DESKTOP_DIR/SmartLampDesktop" "$DEB_ROOT/usr/bin/SmartLampDesktop"
        ln -sf "/usr/bin/SmartLampDesktop" "$DEB_ROOT/usr/bin/smartlamp-studio"
        cp -f "$CLI_DIR/smartlamp" "$DEB_ROOT/usr/bin/smartlamp"
        chmod +x "$DEB_ROOT/usr/bin/SmartLampDesktop" "$DEB_ROOT/usr/bin/smartlamp"

        cp -f "$SRC_APP/Assets/app_icon.png" "$DEB_ROOT/usr/share/icons/hicolor/512x512/apps/smartlamp.png"
        cp -f "$SRC_APP/Assets/app_icon.png" "$DEB_ROOT/usr/share/pixmaps/smartlamp.png"

        cat << EOF > "$DEB_ROOT/usr/share/applications/smartlamp.desktop"
[Desktop Entry]
Type=Application
Name=SmartLamp Studio
Comment=Control Tuya Smart Lamps Locally with Sub-35ms Latency
Exec=/usr/bin/SmartLampDesktop
Icon=smartlamp
Terminal=false
Categories=Utility;HardwareSettings;
Keywords=tuya;smartlamp;light;lamp;
StartupNotify=true
EOF

        INSTALLED_SIZE=$(du -sk "$DEB_ROOT" | cut -f1)

        cat << EOF > "$DEB_ROOT/DEBIAN/control"
Package: smartlamp-studio
Version: $VERSION
Section: utils
Priority: optional
Architecture: $DEB_ARCH
Installed-Size: $INSTALLED_SIZE
Maintainer: Murr <vtstv@users.noreply.github.com>
Homepage: https://github.com/vtstv/SmartLamp
Description: SmartLamp Studio - High-performance Tuya Smart Lamp Controller
 High-performance desktop lighting control application with sub-35ms local LAN
 socket communication, Tuya Cloud fallback, scenes, and presets.
EOF

        dpkg-deb --build --root-owner-group "$DEB_ROOT" "$OUT_DIR/smartlamp-studio_${VERSION}_${DEB_ARCH}.deb"
        echo "--> Generated: $OUT_DIR/smartlamp-studio_${VERSION}_${DEB_ARCH}.deb"
    fi

    # 5. Create CachyOS / Arch Linux Package (.pkg.tar.zst)
    echo "--> Building CachyOS/Arch package (.pkg.tar.zst) for $ARCH_ARCH..."
    ARCH_ROOT="$BUILD_DIR/$RID/arch-root"
    mkdir -p "$ARCH_ROOT/usr/bin" "$ARCH_ROOT/usr/share/applications" \
             "$ARCH_ROOT/usr/share/icons/hicolor/512x512/apps" "$ARCH_ROOT/usr/share/pixmaps"

    cp -f "$DESKTOP_DIR/SmartLampDesktop" "$ARCH_ROOT/usr/bin/SmartLampDesktop"
    ln -sf "/usr/bin/SmartLampDesktop" "$ARCH_ROOT/usr/bin/smartlamp-studio"
    cp -f "$CLI_DIR/smartlamp" "$ARCH_ROOT/usr/bin/smartlamp"
    chmod +x "$ARCH_ROOT/usr/bin/SmartLampDesktop" "$ARCH_ROOT/usr/bin/smartlamp"

    cp -f "$SRC_APP/Assets/app_icon.png" "$ARCH_ROOT/usr/share/icons/hicolor/512x512/apps/smartlamp.png"
    cp -f "$SRC_APP/Assets/app_icon.png" "$ARCH_ROOT/usr/share/pixmaps/smartlamp.png"

    cat << EOF > "$ARCH_ROOT/usr/share/applications/smartlamp.desktop"
[Desktop Entry]
Type=Application
Name=SmartLamp Studio
Comment=Control Tuya Smart Lamps Locally with Sub-35ms Latency
Exec=/usr/bin/SmartLampDesktop
Icon=smartlamp
Terminal=false
Categories=Utility;HardwareSettings;
Keywords=tuya;smartlamp;light;lamp;
StartupNotify=true
EOF

    RAW_SIZE=$(du -sb "$ARCH_ROOT" | cut -f1)
    BUILD_DATE=$(date +%s)

    cat << EOF > "$ARCH_ROOT/.PKGINFO"
pkgname = smartlamp-studio
pkgbase = smartlamp-studio
pkgver = $VERSION-1
pkgdesc = SmartLamp Studio - High-performance Tuya Smart Lamp Controller with sub-35ms response time
url = https://github.com/vtstv/SmartLamp
builddate = $BUILD_DATE
packager = Murr <vtstv@users.noreply.github.com>
size = $RAW_SIZE
arch = $ARCH_ARCH
license = MIT
depend = fontconfig
depend = glibc
depend = libx11
depend = libice
depend = libsm
EOF

    (cd "$ARCH_ROOT" && tar -cf - .PKGINFO usr | zstd -19 -T0 -o "$OUT_DIR/smartlamp-studio-${VERSION}-1-${ARCH_ARCH}.pkg.tar.zst")
    echo "--> Generated: $OUT_DIR/smartlamp-studio-${VERSION}-1-${ARCH_ARCH}.pkg.tar.zst"
done

echo "=================================================================="
echo "==> Linux packaging completed successfully! Generated packages:"
ls -lh "$OUT_DIR"
echo "=================================================================="
