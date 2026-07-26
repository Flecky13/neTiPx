#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_PATH="${ROOT_DIR}/src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj"
PACKAGE_DIR="${ROOT_DIR}/packages"

mkdir -p "${ROOT_DIR}/release-assets" "${PACKAGE_DIR}"

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT_DIR}/src/Directory.Build.props" | head -n1)"

if [[ -z "${VERSION}" ]]; then
    echo "❌ Unable to determine version."
    exit 1
fi

ARCH="$(uname -m)"

if [[ "${ARCH}" == "arm64" ]]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi

OUTPUT_DIR="${ROOT_DIR}/publish/${RID}"

echo "📦 Publishing ${RID}..."

dotnet publish "${PROJECT_PATH}" \
    -c Release \
    -r "${RID}" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=false \
    -o "${OUTPUT_DIR}"

echo
echo "==============================="
echo "Publish directory:"
ls -lah "${OUTPUT_DIR}"
echo "==============================="
echo

#
# Automatically determine executable
#
EXECUTABLE="$(find "${OUTPUT_DIR}" -maxdepth 1 -type f -perm -111 | head -n1)"

if [[ -z "${EXECUTABLE}" ]]; then
    echo "❌ No executable found."
    exit 1
fi

EXECUTABLE_NAME="$(basename "${EXECUTABLE}")"

echo "Executable:"
echo "  ${EXECUTABLE_NAME}"

APP_NAME="neTiPx"
APP_BUNDLE="${PACKAGE_DIR}/${APP_NAME}.app"

rm -rf "${APP_BUNDLE}"

mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

#
# Copy complete publish directory
#
cp -R "${OUTPUT_DIR}/." "${APP_BUNDLE}/Contents/MacOS/"

chmod +x "${APP_BUNDLE}/Contents/MacOS/${EXECUTABLE_NAME}"

cat > "${APP_BUNDLE}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
"http://www.apple.com/DTDs/PropertyList-1.0.dtd">

<plist version="1.0">
<dict>

    <key>CFBundleDevelopmentRegion</key>
    <string>English</string>

    <key>CFBundleExecutable</key>
    <string>${EXECUTABLE_NAME}</string>

    <key>CFBundleIdentifier</key>
    <string>com.netipx.app</string>

    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>

    <key>CFBundleName</key>
    <string>${APP_NAME}</string>

    <key>CFBundleDisplayName</key>
    <string>${APP_NAME}</string>

    <key>CFBundlePackageType</key>
    <string>APPL</string>

    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>

    <key>CFBundleVersion</key>
    <string>${VERSION}</string>

    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>

    <key>NSHighResolutionCapable</key>
    <true/>

</dict>
</plist>
EOF

#
# Ad-hoc signing (no Apple account required)
#
codesign \
    --force \
    --deep \
    --sign - \
    "${APP_BUNDLE}" || true

#
# Smoke test
#
echo
echo "Bundle contents:"
find "${APP_BUNDLE}" | head -50
echo

DMG_TEMP="${PACKAGE_DIR}/dmg-temp"
DMG_FILE="${PACKAGE_DIR}/neTiPx-${VERSION}-${RID}.dmg"

rm -rf "${DMG_TEMP}"
mkdir -p "${DMG_TEMP}"

cp -R "${APP_BUNDLE}" "${DMG_TEMP}/"
ln -s /Applications "${DMG_TEMP}/Applications"

hdiutil create \
    -volname "neTiPx" \
    -srcfolder "${DMG_TEMP}" \
    -format UDZO \
    -ov \
    "${DMG_FILE}"

rm -rf "${DMG_TEMP}"
rm -rf "${OUTPUT_DIR}"

cp "${DMG_FILE}" "${ROOT_DIR}/release-assets/"

echo
echo "✅ Created:"
echo "  ${DMG_FILE}"