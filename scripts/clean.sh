#!/bin/bash
# ============================================================================
# SmartLamp Studio - Deep Clean Script
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "==> Cleaning SmartLamp Studio build artifacts..."

# Remove bin and obj directories
find "$ROOT_DIR" -type d \( -name "bin" -o -name "obj" \) -not -path "*/.*" -exec rm -rf {} + 2>/dev/null || true

# Clean dist directory
if [ -d "$ROOT_DIR/dist" ]; then
    rm -rf "$ROOT_DIR/dist"/*
fi

# Clean logs and tmp files
find "$ROOT_DIR" -type f \( -name "*.log" -o -name "*.tmp" \) -not -path "*/.*" -delete 2>/dev/null || true

# If --full is supplied, clean releases as well
if [ "$1" == "--full" ] && [ -d "$ROOT_DIR/releases" ]; then
    rm -rf "$ROOT_DIR/releases"/*
    echo "  Cleaned releases/ (--full specified)"
fi

echo "==> Workspace cleaned successfully."
