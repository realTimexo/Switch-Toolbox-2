#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -m)" != "x86_64" ]]; then
  echo "This packaging script must run on an x86_64 Linux host." >&2
  exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/LinuxPort/SwitchToolbox.CrossPlatform.csproj"
APP_NAME="SwitchToolbox.CrossPlatform"
PACKAGE_NAME="SwitchToolbox2-Linux-x86_64"
DIST="$ROOT/LinuxPort/dist"
APPDIR="$ROOT/LinuxPort/AppDir"

rm -rf "$DIST" "$APPDIR"
mkdir -p "$DIST" "$APPDIR/usr/bin" "$APPDIR/usr/share/icons/hicolor/256x256/apps"

dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained true -o "$APPDIR/usr/bin"
cp "$ROOT/LinuxPort/assets/switch-toolbox-2.png" \
   "$APPDIR/usr/share/icons/hicolor/256x256/apps/switch-toolbox-2.png"

cat > "$APPDIR/switch-toolbox-2.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=Switch Toolbox 2 (Cross-platform Preview)
Comment=Work-in-progress cross-platform client for Switch Toolbox 2
Exec=AppRun %F
Icon=switch-toolbox-2
Terminal=false
Categories=Development;Utility;
DESKTOP

cat > "$APPDIR/AppRun" <<'APPRUN'
#!/usr/bin/env sh
HERE="$(dirname "$(readlink -f "$0")")"
export LD_LIBRARY_PATH="$HERE/usr/bin${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$HERE/usr/bin/SwitchToolbox.CrossPlatform" "$@"
APPRUN
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/$APP_NAME"

mkdir -p "$DIST/$PACKAGE_NAME"
cp -a "$APPDIR/usr/bin/." "$DIST/$PACKAGE_NAME/"
mkdir -p "$DIST/$PACKAGE_NAME/share/icons"
cp "$ROOT/LinuxPort/assets/switch-toolbox-2.png" "$DIST/$PACKAGE_NAME/share/icons/switch-toolbox-2.png"
cat > "$DIST/$PACKAGE_NAME/README.txt" <<'README'
Switch Toolbox 2 cross-platform client — work in progress

This preview contains only the Avalonia shell and file/folder browser. It does not yet include Nintendo format editors, game-file processing, or the MKW Music Editor. It is not a complete Switch Toolbox 2 release.

Run ./SwitchToolbox.CrossPlatform. A .NET runtime is bundled. Desktop Linux systems need X11/Fontconfig libraries supported by Avalonia.
README
cat > "$DIST/$PACKAGE_NAME/run-switch-toolbox-2.sh" <<'RUNNER'
#!/usr/bin/env sh
HERE="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
export LD_LIBRARY_PATH="$HERE${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$HERE/SwitchToolbox.CrossPlatform" "$@"
RUNNER
chmod +x "$DIST/$PACKAGE_NAME/run-switch-toolbox-2.sh"
tar -C "$DIST" -czf "$DIST/$PACKAGE_NAME.tar.gz" "$PACKAGE_NAME"

APPIMAGETOOL="${APPIMAGETOOL:-$DIST/appimagetool-x86_64.AppImage}"
if [[ ! -x "$APPIMAGETOOL" ]]; then
  curl -fL --retry 3 \
    https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage \
    -o "$APPIMAGETOOL"
  chmod +x "$APPIMAGETOOL"
fi

ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 \
  "$APPIMAGETOOL" "$APPDIR" "$DIST/$PACKAGE_NAME.AppImage"
chmod +x "$DIST/$PACKAGE_NAME.AppImage"
printf 'Created preview packages:\n%s\n%s\n' \
  "$DIST/$PACKAGE_NAME.AppImage" "$DIST/$PACKAGE_NAME.tar.gz"
