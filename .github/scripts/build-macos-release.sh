#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_PATH="${ROOT_DIR}/src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj"
PACKAGE_DIR="${ROOT_DIR}/packages"

mkdir -p "${ROOT_DIR}/release-assets" "${PACKAGE_DIR}"

VERSION="$(grep -oP '(?<=<Version>)[^<]+' "${ROOT_DIR}/src/Directory.Build.props" | head -n1)"
if [[ -z "${VERSION}" ]]; then
  echo "Version could not be read from src/Directory.Build.props"
  exit 1
fi

ARCH="$(uname -m)"
if [[ "${ARCH}" == "arm64" ]]; then
  RID="osx-arm64"
else
  RID="osx-x64"
fi

OUTPUT_DIR="${ROOT_DIR}/publish/${RID}"

dotnet publish "${PROJECT_PATH}" \
  -c Release \
  -r "${RID}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUTPUT_DIR}"

chmod +x "${OUTPUT_DIR}/neTiPx.UI.Avalonia"

APP_NAME="neTiPx"
APP_BUNDLE="${PACKAGE_DIR}/${APP_NAME}.app"
rm -rf "${APP_BUNDLE}"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

cp "${OUTPUT_DIR}/neTiPx.UI.Avalonia" "${APP_BUNDLE}/Contents/MacOS/${APP_NAME}"
chmod +x "${APP_BUNDLE}/Contents/MacOS/${APP_NAME}"

cat > "${APP_BUNDLE}/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>com.netipx.app</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>neTiPx</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

if [[ -f "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" ]]; then
  mkdir -p "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.iconset"
  sips -z 512 512 "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" --out "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.iconset/icon_512x512.png" >/dev/null 2>&1 || true
  iconutil -c icns "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.iconset" -o "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.icns" >/dev/null 2>&1 || true
  rm -rf "${APP_BUNDLE}/Contents/Resources/${APP_NAME}.iconset"
fi

DMG_FILE="${PACKAGE_DIR}/neTiPx-${VERSION}-${RID}.dmg"
DMG_TEMP="${PACKAGE_DIR}/dmg-temp"
rm -rf "${DMG_TEMP}"
mkdir -p "${DMG_TEMP}"
cp -R "${APP_BUNDLE}" "${DMG_TEMP}/"
ln -s /Applications "${DMG_TEMP}/Applications"

hdiutil create -volname "neTiPx" \
  -srcfolder "${DMG_TEMP}" \
  -ov \
  -format UDZO \
  "${DMG_FILE}"

rm -rf "${DMG_TEMP}"

dmg_file="$(find "${ROOT_DIR}/packages" -maxdepth 1 -type f -name '*.dmg' -print0 | xargs -0 ls -1t | head -n1)"
if [[ -z "${dmg_file}" ]]; then
  echo "No .dmg file produced."
  exit 1
fi

cp "${dmg_file}" "${ROOT_DIR}/release-assets/"

echo "macOS release asset: $(basename "${dmg_file}")"
