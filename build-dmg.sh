#!/usr/bin/env bash
set -euo pipefail

APP_NAME="ClickLight"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFO_PLIST="$ROOT_DIR/Info.plist"
DIST_DIR="$ROOT_DIR/dist"
STAGING_DIR=""

cleanup() {
    if [[ -n "$STAGING_DIR" && -d "$STAGING_DIR" ]]; then
        rm -rf "$STAGING_DIR"
    fi
}
trap cleanup EXIT

if ! command -v hdiutil >/dev/null 2>&1; then
    echo "error: hdiutil is required to build a DMG" >&2
    exit 1
fi

if [[ ! -x "$ROOT_DIR/build-app.sh" ]]; then
    echo "error: build-app.sh must exist and be executable" >&2
    exit 1
fi

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$INFO_PLIST" 2>/dev/null || true)"
if [[ -z "$VERSION" ]]; then
    echo "error: could not read CFBundleShortVersionString from Info.plist" >&2
    exit 1
fi

DMG_PATH="$DIST_DIR/$APP_NAME-$VERSION.dmg"

"$ROOT_DIR/build-app.sh"

mkdir -p "$DIST_DIR"
rm -f "$DMG_PATH"

STAGING_DIR="$(mktemp -d "${TMPDIR:-/tmp}/${APP_NAME}.dmg.XXXXXX")"
cp -R "$ROOT_DIR/$APP_NAME.app" "$STAGING_DIR/$APP_NAME.app"
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$STAGING_DIR" \
    -ov \
    -format UDZO \
    "$DMG_PATH"

echo "Built $DMG_PATH"
