#!/bin/bash
# ============================================================================
# SmartLamp Studio - Unified Build Script (Bash / Linux / macOS)
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
DIST_DIR="$ROOT_DIR/dist"
RELEASES_DIR="$ROOT_DIR/releases"

TARGET="all"
CONFIG="Release"
ARCH="x64"
PACK=false
CLEAN=false

# Print Help
show_help() {
    cat << EOF
Usage: ./scripts/build.sh [OPTIONS]

Options:
  --target <all|desktop|cli|lib>  Build target (default: all)
  --config <Release|Debug>        Build configuration (default: Release)
  --arch <x64|arm64|all>          Target architecture (default: x64)
  --pack                          Create release packages in releases/
  --clean                         Clean build outputs before compilation
  --help                          Show this help message
EOF
    exit 0
}

# Parse Arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --target) TARGET="$2"; shift ;;
        --config) CONFIG="$2"; shift ;;
        --arch) ARCH="$2"; shift ;;
        --pack) PACK=true ;;
        --clean) CLEAN=true ;;
        --help) show_help ;;
        *) echo "Unknown parameter: $1"; show_help ;;
    esac
    shift
done

# Clean if requested
if [ "$CLEAN" = true ]; then
    bash "$SCRIPT_DIR/clean.sh"
fi

# Extract Version
VERSION="3.1.0"
if [ -f "$ROOT_DIR/src/AppVersion.cs" ]; then
    VERSION=$(grep 'public const string Version' "$ROOT_DIR/src/AppVersion.cs" | head -n 1 | sed -E 's/.*"([^"]+)".*/\1/')
fi

echo "================================================================"
echo "      SmartLamp Studio Build System v$VERSION"
echo "      Target: $TARGET | Config: $CONFIG | Arch: $ARCH | Pack: $PACK"
echo "================================================================"

mkdir -p "$DIST_DIR"
if [ "$PACK" = true ]; then
    mkdir -p "$RELEASES_DIR"
fi

# Resolve RIDs
RIDS=()
case "$ARCH" in
    x64) RIDS=("linux-x64") ;;
    arm64) RIDS=("linux-arm64") ;;
    all) RIDS=("linux-x64" "linux-arm64") ;;
    *) echo "Invalid arch: $ARCH"; exit 1 ;;
esac

# 1. Build Library
if [ "$TARGET" = "all" ] || [ "$TARGET" = "lib" ]; then
    echo -e "\n[1] Building TuyaNet.Native Protocol Library..."
    dotnet build "$ROOT_DIR/libs/TuyaNet.Native/TuyaNet.Native.csproj" -c "$CONFIG"
    if [ "$PACK" = true ]; then
        dotnet pack "$ROOT_DIR/libs/TuyaNet.Native/TuyaNet.Native.csproj" -c "$CONFIG" -o "$RELEASES_DIR"
    fi
fi

# 2. Build CLI
if [ "$TARGET" = "all" ] || [ "$TARGET" = "cli" ]; then
    echo -e "\n[2] Building Native CLI..."
    for rid in "${RIDS[@]}"; do
        echo "  --> Compiling CLI for $rid..."
        dotnet publish "$ROOT_DIR/src/SmartLamp.Cli/SmartLamp.Cli.csproj" \
            -c "$CONFIG" -r "$rid" --self-contained \
            -p:PublishSingleFile=true -p:PublishTrimmed=true \
            -o "$DIST_DIR/cli-$rid"
    done
fi

# 3. Build Desktop
if [ "$TARGET" = "all" ] || [ "$TARGET" = "desktop" ]; then
    echo -e "\n[3] Building Avalonia Desktop..."
    for rid in "${RIDS[@]}"; do
        echo "  --> Compiling Desktop for $rid..."
        dotnet publish "$ROOT_DIR/src/SmartLamp.Desktop/SmartLamp.Desktop.csproj" \
            -c "$CONFIG" -r "$rid" --self-contained \
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
            -o "$DIST_DIR/desktop-$rid"
    done
fi

# 4. Pack Multi-Distro packages if requested
if [ "$PACK" = true ] && [ "$TARGET" != "lib" ]; then
    echo -e "\n[4] Packaging Linux Distributions (.deb, .pkg.tar.zst, .tar.gz)..."
    bash "$SCRIPT_DIR/package-linux.sh"
fi

echo -e "\n================================================================"
echo "      BUILD COMPLETED SUCCESSFULLY!"
echo "================================================================"
