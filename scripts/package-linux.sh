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
PKG_TEMPLATES="$ROOT_DIR/packaging"

# Detect version
VERSION="3.2.0"
if [ -f "$ROOT_DIR/VERSION" ]; then
    VERSION=$(cat "$ROOT_DIR/VERSION" | tr -d '\r\n')
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
    
    cp -f "$PKG_TEMPLATES/desktop/smartlamp.desktop" "$BUNDLE_DIR/"
    cp -f "$PKG_TEMPLATES/scripts/install.sh" "$BUNDLE_DIR/"
    cp -f "$PKG_TEMPLATES/scripts/uninstall.sh" "$BUNDLE_DIR/"
    chmod +x "$BUNDLE_DIR/install.sh" "$BUNDLE_DIR/uninstall.sh"

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
        cp -f "$PKG_TEMPLATES/desktop/smartlamp.desktop" "$DEB_ROOT/usr/share/applications/smartlamp.desktop"
        sed -i 's|Exec=SmartLampDesktop|Exec=/usr/bin/SmartLampDesktop|g' "$DEB_ROOT/usr/share/applications/smartlamp.desktop"

        INSTALLED_SIZE=$(du -sk "$DEB_ROOT" | cut -f1)

        sed -e "s|{{VERSION}}|$VERSION|g" \
            -e "s|{{ARCH}}|$DEB_ARCH|g" \
            -e "s|{{INSTALLED_SIZE}}|$INSTALLED_SIZE|g" \
            "$PKG_TEMPLATES/debian/control.template" > "$DEB_ROOT/DEBIAN/control"

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
    cp -f "$PKG_TEMPLATES/desktop/smartlamp.desktop" "$ARCH_ROOT/usr/share/applications/smartlamp.desktop"
    sed -i 's|Exec=SmartLampDesktop|Exec=/usr/bin/SmartLampDesktop|g' "$ARCH_ROOT/usr/share/applications/smartlamp.desktop"

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
