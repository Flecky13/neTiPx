#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_PATH="${ROOT_DIR}/src/neTiPx.UI.Avalonia/neTiPx.UI.Avalonia.csproj"
OUTPUT_DIR="${ROOT_DIR}/publish/linux-x64"
PACKAGE_DIR="${ROOT_DIR}/packages"

mkdir -p "${ROOT_DIR}/release-assets" "${PACKAGE_DIR}"

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_HOME="${ROOT_DIR}/.dotnet"
export NUGET_PACKAGES="${ROOT_DIR}/.nuget/packages"
mkdir -p "${DOTNET_CLI_HOME}" "${NUGET_PACKAGES}"

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT_DIR}/src/Directory.Build.props" | head -n1)"
if [[ -z "${VERSION}" ]]; then
  echo "Version could not be read from src/Directory.Build.props"
  exit 1
fi

dotnet publish "${PROJECT_PATH}" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUTPUT_DIR}"

chmod +x "${OUTPUT_DIR}/neTiPx.UI.Avalonia"

DEB_DIR="${PACKAGE_DIR}/netipx-deb"
rm -rf "${DEB_DIR}"
mkdir -p "${DEB_DIR}/DEBIAN"
mkdir -p "${DEB_DIR}/usr/local/bin"
mkdir -p "${DEB_DIR}/opt/netipx"
mkdir -p "${DEB_DIR}/usr/share/applications"
mkdir -p "${DEB_DIR}/usr/share/icons/hicolor/256x256/apps"

cp -a "${OUTPUT_DIR}/." "${DEB_DIR}/opt/netipx/"
chmod +x "${DEB_DIR}/opt/netipx/neTiPx.UI.Avalonia"

cat > "${DEB_DIR}/usr/local/bin/netipx" << 'EOF'
#!/bin/bash
exec /opt/netipx/neTiPx.UI.Avalonia "$@"
EOF
chmod +x "${DEB_DIR}/usr/local/bin/netipx"

cat > "${DEB_DIR}/usr/share/applications/netipx.desktop" << 'EOF'
[Desktop Entry]
Type=Application
Name=neTiPx
Comment=Network Tools and IP Configuration Manager
Exec=/usr/local/bin/netipx
Icon=netipx
Terminal=false
Categories=Network;System;Utility;
StartupWMClass=neTiPx.UI.Avalonia
EOF

if [[ -f "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" ]]; then
  cp "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" \
    "${DEB_DIR}/usr/share/icons/hicolor/256x256/apps/netipx.png"
fi

cat > "${DEB_DIR}/DEBIAN/control" << EOF
Package: netipx
Version: ${VERSION}
Section: net
Priority: optional
Architecture: amd64
Maintainer: neTiPx Developer <developer@netipx.local>
Description: Network Tools and IP Configuration Manager
 neTiPx is a modern desktop tool for comfortable management
 of network adapters and IP configurations.
EOF

cat > "${DEB_DIR}/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e
if command -v update-desktop-database &> /dev/null; then
  update-desktop-database -q
fi
if command -v gtk-update-icon-cache &> /dev/null; then
  gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi
exit 0
EOF
chmod +x "${DEB_DIR}/DEBIAN/postinst"

cat > "${DEB_DIR}/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e
if [[ "$1" == "remove" || "$1" == "purge" ]]; then
  if command -v update-desktop-database &> /dev/null; then
    update-desktop-database -q
  fi
  if command -v gtk-update-icon-cache &> /dev/null; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
  fi
fi
exit 0
EOF
chmod +x "${DEB_DIR}/DEBIAN/postrm"

DEB_FILE="${PACKAGE_DIR}/netipx_${VERSION}_amd64.deb"
dpkg-deb --build "${DEB_DIR}" "${DEB_FILE}"

APPDIR="${PACKAGE_DIR}/neTiPx.AppDir"
rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin"
cp "${OUTPUT_DIR}/neTiPx.UI.Avalonia" "${APPDIR}/usr/bin/netipx"
chmod +x "${APPDIR}/usr/bin/netipx"

cat > "${APPDIR}/AppRun" << 'EOF'
#!/bin/bash
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/netipx" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

cat > "${APPDIR}/netipx.desktop" << 'EOF'
[Desktop Entry]
Type=Application
Name=neTiPx
Comment=Network Tools and IP Configuration Manager
Exec=netipx
Icon=netipx
Categories=Network;System;
EOF

if [[ -f "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" ]]; then
  cp "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" "${APPDIR}/netipx.png"
  cp "${ROOT_DIR}/src/neTiPx.UI.Avalonia/Assets/toolicon.png" "${APPDIR}/.DirIcon"
fi

APPIMAGE_FILE="${PACKAGE_DIR}/neTiPx-${VERSION}-x86_64.AppImage"
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "${ROOT_DIR}/.github/scripts/appimagetool-x86_64.AppImage" "${APPDIR}" "${APPIMAGE_FILE}" || \
  ARCH=x86_64 "${ROOT_DIR}/.github/scripts/appimagetool-x86_64.AppImage" "${APPDIR}" "${APPIMAGE_FILE}"

deb_file="$(find "${ROOT_DIR}/packages" -maxdepth 1 -type f -name '*.deb' -printf '%T@ %p\n' | sort -nr | head -n1 | cut -d' ' -f2-)"
appimage_file="$(find "${ROOT_DIR}/packages" -maxdepth 1 -type f -name '*.AppImage' -printf '%T@ %p\n' | sort -nr | head -n1 | cut -d' ' -f2-)"

if [[ -z "${deb_file}" ]]; then
  echo "No .deb file produced."
  exit 1
fi

if [[ -z "${appimage_file}" ]]; then
  echo "No AppImage file produced."
  exit 1
fi

cp "${deb_file}" "${ROOT_DIR}/release-assets/"
cp "${appimage_file}" "${ROOT_DIR}/release-assets/"

echo "Linux release assets: $(basename "${deb_file}"), $(basename "${appimage_file}")"
