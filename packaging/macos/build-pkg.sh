#!/bin/bash
set -euo pipefail

VERSION=""
ARTIFACTS_PATH=""
OUTPUT_PATH="./output"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --artifacts-path) ARTIFACTS_PATH="$2"; shift 2 ;;
        --output-path) OUTPUT_PATH="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$VERSION" || -z "$ARTIFACTS_PATH" ]]; then
    echo "Usage: $0 --version <version> --artifacts-path <path> [--output-path <path>]"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STAGING="$(mktemp -d)"
COMPONENT_PKGS="$(mktemp -d)"

trap 'rm -rf "$STAGING" "$COMPONENT_PKGS"' EXIT

echo "=== Building NoMercy MediaServer macOS installer v${VERSION} ==="

# --- Component 1: Server ---
echo "Staging Server component..."
SERVER_ROOT="${STAGING}/server"
mkdir -p "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS"
mkdir -p "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/Resources"

cp "${ARTIFACTS_PATH}/NoMercyMediaServer-macos-x64" \
   "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS/NoMercyMediaServer"
chmod 755 "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS/NoMercyMediaServer"

# Copy icon if available
if [[ -f "${SCRIPT_DIR}/../../assets/icons/icon.icns" ]]; then
    cp "${SCRIPT_DIR}/../../assets/icons/icon.icns" \
       "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/Resources/AppIcon.icns"
fi

cat > "${SERVER_ROOT}/Applications/NoMercy MediaServer.app/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>NoMercyMediaServer</string>
    <key>CFBundleIdentifier</key>
    <string>tv.nomercy.mediaserver</string>
    <key>CFBundleName</key>
    <string>NoMercy MediaServer</string>
    <key>CFBundleDisplayName</key>
    <string>NoMercy MediaServer</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSUIElement</key>
    <true/>
</dict>
</plist>
PLIST

pkgbuild --root "${SERVER_ROOT}" \
    --identifier tv.nomercy.mediaserver \
    --version "${VERSION}" \
    --install-location "/" \
    "${COMPONENT_PKGS}/server.pkg"

# --- Component 2: Service ---
echo "Staging Service component..."
SERVICE_ROOT="${STAGING}/service"
mkdir -p "${SERVICE_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS"
mkdir -p "${SERVICE_ROOT}/Library/LaunchAgents"

cp "${ARTIFACTS_PATH}/NoMercyMediaServerService-macos-x64" \
   "${SERVICE_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS/NoMercyMediaServerService"
chmod 755 "${SERVICE_ROOT}/Applications/NoMercy MediaServer.app/Contents/MacOS/NoMercyMediaServerService"

cp "${SCRIPT_DIR}/tv.nomercy.mediaserver.service.plist" \
   "${SERVICE_ROOT}/Library/LaunchAgents/tv.nomercy.mediaserver.service.plist"

pkgbuild --root "${SERVICE_ROOT}" \
    --identifier tv.nomercy.mediaserver.service \
    --version "${VERSION}" \
    --install-location "/" \
    "${COMPONENT_PKGS}/service.pkg"

# --- Component 3: CLI ---
echo "Staging CLI component..."
CLI_ROOT="${STAGING}/cli"
mkdir -p "${CLI_ROOT}/usr/local/bin"

cp "${ARTIFACTS_PATH}/nomercy-macos-x64" \
   "${CLI_ROOT}/usr/local/bin/nomercy"
chmod 755 "${CLI_ROOT}/usr/local/bin/nomercy"

pkgbuild --root "${CLI_ROOT}" \
    --identifier tv.nomercy.cli \
    --version "${VERSION}" \
    --install-location "/" \
    "${COMPONENT_PKGS}/cli.pkg"

# --- Build combined installer ---
echo "Building combined .pkg installer..."
mkdir -p "${OUTPUT_PATH}"

productbuild \
    --distribution "${SCRIPT_DIR}/distribution.xml" \
    --package-path "${COMPONENT_PKGS}" \
    --version "${VERSION}" \
    "${OUTPUT_PATH}/NoMercyMediaServer-${VERSION}-macos-x64-setup.pkg"

echo "=== macOS installer built: ${OUTPUT_PATH}/NoMercyMediaServer-${VERSION}-macos-x64-setup.pkg ==="
