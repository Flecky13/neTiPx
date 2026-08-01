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

        ARCH_X64="$(lipo -archs "${FILE}" 2>/dev/null || true)"
        ARCH_ARM64="$(lipo -archs "${ARM_FILE}" 2>/dev/null || true)"

        if [[ -n "${ARCH_X64}" && "${ARCH_X64}" == "${ARCH_ARM64}" ]]; then
            echo "Skip universal merge (same arch: ${REL} -> ${ARCH_X64})"
            continue
        fi

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
APP_EXECUTABLE_NAME="neTiPx.UI.Avalonia"
create_app_bundle() {
    local source_dir="$1"
    local app_bundle="$2"

    rm -rf "${app_bundle}"

    mkdir -p "${app_bundle}/Contents/MacOS"
    mkdir -p "${app_bundle}/Contents/Resources"

    cp -R "${source_dir}/." "${app_bundle}/Contents/MacOS/"

    local executable="${app_bundle}/Contents/MacOS/${APP_EXECUTABLE_NAME}"

    if [[ -z "${executable}" || ! -f "${executable}" ]]; then
        echo "❌ Expected executable not found: ${APP_EXECUTABLE_NAME}"
        exit 1
    fi

    local executable_name
    executable_name="$(basename "${executable}")"

    cat > "${app_bundle}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
"http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>

<key>CFBundleDevelopmentRegion</key>
<string>English</string>

<key>CFBundleExecutable</key>
<string>${executable_name}</string>

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

    codesign \
        --force \
        --deep \
        --sign - \
        "${app_bundle}" || true

    echo "${executable}"
}

create_dmg_from_bundle() {
    local app_bundle="$1"
    local dmg_file="$2"

    local dmg_temp="${PACKAGE_DIR}/dmg-temp-$(basename "${dmg_file}" .dmg)"

    rm -rf "${dmg_temp}"
    mkdir -p "${dmg_temp}"

    cp -R "${app_bundle}" "${dmg_temp}/"
    ln -s /Applications "${dmg_temp}/Applications"

    hdiutil create \
        -volname "neTiPx" \
        -srcfolder "${dmg_temp}" \
        -format UDZO \
        -ov \
        "${dmg_file}"

    rm -rf "${dmg_temp}"
    cp "${dmg_file}" "${RELEASE_DIR}/"
}

APP_BUNDLE_UNIVERSAL="${PACKAGE_DIR}/${APP_NAME}.app"

DMG_FILE_UNIVERSAL="${PACKAGE_DIR}/neTiPx-${VERSION}-macOS.dmg"

############################################################
# Signieren
############################################################

echo
echo "Signing..."

EXECUTABLE_UNIVERSAL="$(create_app_bundle "${UNIVERSAL_DIR}" "${APP_BUNDLE_UNIVERSAL}")"

############################################################
# Kontrolle
############################################################

echo
echo "Universal executable:"

lipo -info "${EXECUTABLE_UNIVERSAL}" || true

############################################################
# DMG
############################################################

create_dmg_from_bundle "${APP_BUNDLE_UNIVERSAL}" "${DMG_FILE_UNIVERSAL}"

echo
echo "=========================================="
echo "SUCCESS"
echo "=========================================="

echo
echo "Created:"
echo "${DMG_FILE_UNIVERSAL}"

echo
lipo -info "${EXECUTABLE_UNIVERSAL}"

rm -rf "${ROOT_DIR}/publish"