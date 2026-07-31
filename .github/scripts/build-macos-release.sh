#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_PATH="${ROOT_DIR}/src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj"

PACKAGE_DIR="${ROOT_DIR}/packages"
RELEASE_DIR="${ROOT_DIR}/release-assets"

mkdir -p "${PACKAGE_DIR}"
mkdir -p "${RELEASE_DIR}"

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT_DIR}/src/Directory.Build.props" | head -n1)"

if [[ -z "${VERSION}" ]]; then
    echo "❌ Unable to determine version."
    exit 1
fi

X64_DIR="${ROOT_DIR}/publish/osx-x64"
ARM64_DIR="${ROOT_DIR}/publish/osx-arm64"
UNIVERSAL_DIR="${ROOT_DIR}/publish/universal"

rm -rf "${ROOT_DIR}/publish"
mkdir -p "${UNIVERSAL_DIR}"

############################################################
# Publish
############################################################

for RID in osx-x64 osx-arm64
do
    echo
    echo "=========================================="
    echo "Publishing ${RID}"
    echo "=========================================="

    dotnet publish "${PROJECT_PATH}" \
        -c Release \
        -r "${RID}" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:IncludeNativeLibrariesForSelfExtract=false \
        -o "${ROOT_DIR}/publish/${RID}"
done

############################################################
# Universal Publish vorbereiten
############################################################

echo
echo "Creating universal publish..."

cp -R "${X64_DIR}/." "${UNIVERSAL_DIR}/"

############################################################
# Alle Mach-O Dateien zusammenführen
############################################################

find "${X64_DIR}" -type f | while read FILE
do
    REL="${FILE#${X64_DIR}/}"

    ARM_FILE="${ARM64_DIR}/${REL}"
    OUT_FILE="${UNIVERSAL_DIR}/${REL}"

    [[ -f "${ARM_FILE}" ]] || continue

    TYPE=$(file -b "${FILE}")

    if [[ "${TYPE}" == *"Mach-O"* ]]; then

        echo "Universal: ${REL}"

        mkdir -p "$(dirname "${OUT_FILE}")"

        lipo -create \
            "${FILE}" \
            "${ARM_FILE}" \
            -output "${OUT_FILE}"

        chmod +x "${OUT_FILE}"
    fi
done

############################################################
# App Bundle
############################################################

APP_NAME="neTiPx"
APP_BUNDLE="${PACKAGE_DIR}/${APP_NAME}.app"

rm -rf "${APP_BUNDLE}"

mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

cp -R "${UNIVERSAL_DIR}/." "${APP_BUNDLE}/Contents/MacOS/"

EXECUTABLE="${APP_BUNDLE}/Contents/MacOS/${APP_NAME}"

if [[ ! -f "${EXECUTABLE}" ]]; then
    EXECUTABLE="$(find "${APP_BUNDLE}/Contents/MacOS" -maxdepth 1 -type f -perm -111 | head -n1)"
fi

EXECUTABLE_NAME="$(basename "${EXECUTABLE}")"

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

<key>CFBundlePackageType</key>
<string>APPL</string>

<key>CFBundleName</key>
<string>${APP_NAME}</string>

<key>CFBundleDisplayName</key>
<string>${APP_NAME}</string>

<key>CFBundleVersion</key>
<string>${VERSION}</string>

<key>CFBundleShortVersionString</key>
<string>${VERSION}</string>

<key>LSMinimumSystemVersion</key>
<string>10.15</string>

<key>NSHighResolutionCapable</key>
<true/>

</dict>
</plist>
EOF

############################################################
# Signieren
############################################################

echo
echo "Signing..."

codesign \
    --force \
    --deep \
    --sign - \
    "${APP_BUNDLE}" || true

############################################################
# Kontrolle
############################################################

echo
echo "Universal executable:"

lipo -info "${EXECUTABLE}" || true

############################################################
# DMG
############################################################

DMG_TEMP="${PACKAGE_DIR}/dmg-temp"

DMG_FILE="${PACKAGE_DIR}/neTiPx-${VERSION}-macOS.dmg"

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

cp "${DMG_FILE}" "${RELEASE_DIR}/"

echo
echo "=========================================="
echo "SUCCESS"
echo "=========================================="

echo
echo "Created:"
echo "${DMG_FILE}"

echo
lipo -info "${EXECUTABLE}"

rm -rf "${ROOT_DIR}/publish"