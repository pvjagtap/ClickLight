#!/usr/bin/env bash
set -euo pipefail

APP_NAME="ClickLight"
BUNDLE_IDENTIFIER="com.aurorascharff.ClickLight"
CONFIGURATION="${CONFIGURATION:-release}"
NOTARIZE=false

for arg in "$@"; do
    case "$arg" in
        --release)
            CONFIGURATION="release"
            ;;
        --notarize)
            NOTARIZE=true
            ;;
        *)
            echo "Unknown argument: $arg"
            exit 1
            ;;
    esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$ROOT_DIR/.build/$CONFIGURATION"
APP_DIR="$ROOT_DIR/$APP_NAME.app"
ZIP_PATH="$ROOT_DIR/$APP_NAME.zip"
ICON_SOURCE="$ROOT_DIR/AppIcon.icon/Assets/ClickLight-icon.png"
ICON_RENDERED_SOURCE="$BUILD_DIR/$APP_NAME-rendered-icon.png"
ICON_RENDERED_SVG="$BUILD_DIR/$APP_NAME-rendered-icon.svg"
ICONSET_DIR="$BUILD_DIR/$APP_NAME.iconset"

VERSION="$(git describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.1.0")"

swift build -c "$CONFIGURATION"

rm -rf "$APP_DIR" "$ZIP_PATH"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources" "$APP_DIR/Contents/Frameworks"

cp "$ROOT_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_IDENTIFIER" "$APP_DIR/Contents/Info.plist"

cp "$BUILD_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"

if [ ! -f "$ICON_SOURCE" ]; then
    echo "Missing app icon source: $ICON_SOURCE"
    exit 1
fi

rm -rf "$ICONSET_DIR"
mkdir -p "$ICONSET_DIR"
ICON_IMAGE_DATA="$(base64 -i "$ICON_SOURCE" | tr -d '\n')"
cat > "$ICON_RENDERED_SVG" <<SVG
<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
  <defs>
    <linearGradient id="background" x1="50%" y1="100%" x2="50%" y2="30%">
      <stop offset="0%" stop-color="#296bd5"/>
      <stop offset="100%" stop-color="#2dc9fc"/>
    </linearGradient>
    <filter id="glyphShadow" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="28" stdDeviation="26" flood-color="#000" flood-opacity="0.5"/>
    </filter>
  </defs>
  <rect width="1024" height="1024" rx="224" fill="url(#background)"/>
  <g filter="url(#glyphShadow)" opacity="0.88">
    <image href="data:image/png;base64,$ICON_IMAGE_DATA" width="1024" height="1024"/>
  </g>
  <path d="M112 0h800c62 0 112 50 112 112v800c0 62-50 112-112 112H112C50 1024 0 974 0 912V112C0 50 50 0 112 0Z" fill="#fff" opacity="0.16"/>
</svg>
SVG
rsvg-convert -w 1024 -h 1024 "$ICON_RENDERED_SVG" -o "$ICON_RENDERED_SOURCE"
sips -z 16 16 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
sips -z 32 32 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
sips -z 32 32 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
sips -z 64 64 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
sips -z 128 128 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
sips -z 256 256 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
sips -z 256 256 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
sips -z 512 512 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
sips -z 512 512 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
sips -z 1024 1024 "$ICON_RENDERED_SOURCE" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
iconutil --convert icns "$ICONSET_DIR" --output "$APP_DIR/Contents/Resources/$APP_NAME.icns"

SPARKLE_PATH="$(find "$ROOT_DIR/.build" -name "Sparkle.framework" -type d | head -1)"
if [ -n "$SPARKLE_PATH" ]; then
    cp -a "$SPARKLE_PATH" "$APP_DIR/Contents/Frameworks/"
    install_name_tool -add_rpath "@executable_path/../Frameworks" "$APP_DIR/Contents/MacOS/$APP_NAME" 2>/dev/null || true
fi

SIGNING_IDENTITY="${SIGNING_IDENTITY:--}"
echo "Code signing with identity: $SIGNING_IDENTITY"

if [ -d "$APP_DIR/Contents/Frameworks/Sparkle.framework" ]; then
    SPARKLE_VERSION_DIR="$APP_DIR/Contents/Frameworks/Sparkle.framework/Versions/B"
    codesign --force --sign "$SIGNING_IDENTITY" "$SPARKLE_VERSION_DIR/Sparkle"
    codesign --force --sign "$SIGNING_IDENTITY" "$SPARKLE_VERSION_DIR/Updater.app"
    find "$SPARKLE_VERSION_DIR/XPCServices" -name "*.xpc" -exec codesign --force --sign "$SIGNING_IDENTITY" {} \;
fi

if [ "$SIGNING_IDENTITY" = "-" ]; then
    codesign --force --deep --sign "$SIGNING_IDENTITY" "$APP_DIR"
else
    codesign --force --deep --options runtime --sign "$SIGNING_IDENTITY" "$APP_DIR"
fi

if [ "$NOTARIZE" = true ]; then
    if [ -z "${APP_STORE_CONNECT_KEY:-}" ] ||
        [ -z "${APP_STORE_CONNECT_KEY_ID:-}" ] ||
        [ -z "${APP_STORE_CONNECT_ISSUER_ID:-}" ]; then
        echo "Notarization requires APP_STORE_CONNECT_KEY, APP_STORE_CONNECT_KEY_ID, and APP_STORE_CONNECT_ISSUER_ID."
        exit 1
    fi

    KEY_FILE="$(mktemp)"
    echo "$APP_STORE_CONNECT_KEY" > "$KEY_FILE"

    ditto -c -k --keepParent "$APP_DIR" "$ZIP_PATH"
    xcrun notarytool submit "$ZIP_PATH" \
        --key "$KEY_FILE" \
        --key-id "$APP_STORE_CONNECT_KEY_ID" \
        --issuer "$APP_STORE_CONNECT_ISSUER_ID" \
        --wait
    xcrun stapler staple "$APP_DIR"

    rm "$KEY_FILE" "$ZIP_PATH"
fi

echo "Built $APP_DIR"
