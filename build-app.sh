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
ICON_BUNDLE="$ROOT_DIR/AppIcon.icon"
ICON_SOURCE="$ICON_BUNDLE/Assets/ClickLight-icon.png"
ICON_RENDER_SCRIPT="$ROOT_DIR/scripts/render-icon.swift"
ICON_RENDERED_SOURCE="$BUILD_DIR/$APP_NAME-rendered-icon.png"
ICONSET_DIR="$BUILD_DIR/$APP_NAME.iconset"
ICON_PARTIAL_PLIST="$BUILD_DIR/AppIcon-partial.plist"
ICON_RESOURCE_NAME="AppIcon"

ensure_fallback_icon_inputs() {
    local missing_context="$1"

    if [ ! -f "$ICON_SOURCE" ]; then
        echo "$missing_context icon source is missing: $ICON_SOURCE"
        exit 1
    fi

    if [ ! -f "$ICON_RENDER_SCRIPT" ]; then
        echo "$missing_context app icon renderer is missing: $ICON_RENDER_SCRIPT"
        exit 1
    fi
}

generate_fallback_icns() {
    rm -rf "$ICONSET_DIR"
    mkdir -p "$ICONSET_DIR"
    swift "$ICON_RENDER_SCRIPT" "$ICON_SOURCE" "$ICON_RENDERED_SOURCE"
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
    iconutil --convert icns "$ICONSET_DIR" --output "$APP_DIR/Contents/Resources/$ICON_RESOURCE_NAME.icns"
}

VERSION="$(git describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.1.0")"

swift build -c "$CONFIGURATION"

rm -rf "$APP_DIR" "$ZIP_PATH"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources" "$APP_DIR/Contents/Frameworks"

cp "$ROOT_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_IDENTIFIER" "$APP_DIR/Contents/Info.plist"

cp "$BUILD_DIR/$APP_NAME" "$APP_DIR/Contents/MacOS/$APP_NAME"

if [ ! -d "$ICON_BUNDLE" ]; then
    echo "Missing app icon bundle: $ICON_BUNDLE"
    exit 1
fi

rm -f "$APP_DIR/Contents/Resources/$ICON_RESOURCE_NAME.icns" "$APP_DIR/Contents/Resources/Assets.car" "$ICON_PARTIAL_PLIST"

if command -v xcrun >/dev/null 2>&1 && xcrun --find actool >/dev/null 2>&1; then
    xcrun actool "$ICON_BUNDLE" \
        --compile "$APP_DIR/Contents/Resources" \
        --app-icon "$ICON_RESOURCE_NAME" \
        --platform macosx \
        --target-device mac \
        --minimum-deployment-target 14.0 \
        --standalone-icon-behavior all \
        --include-all-app-icons \
        --output-partial-info-plist "$ICON_PARTIAL_PLIST" \
        --output-format human-readable-text

    rm -f "$ICON_PARTIAL_PLIST"

    if [ ! -f "$APP_DIR/Contents/Resources/$ICON_RESOURCE_NAME.icns" ]; then
        ensure_fallback_icon_inputs "actool did not produce $ICON_RESOURCE_NAME.icns and fallback"
        generate_fallback_icns
    fi

    if [ ! -f "$APP_DIR/Contents/Resources/$ICON_RESOURCE_NAME.icns" ] && [ ! -f "$APP_DIR/Contents/Resources/Assets.car" ]; then
        echo "actool and fallback generation did not produce an app icon output."
        exit 1
    fi
else
    ensure_fallback_icon_inputs "Missing fallback"
    generate_fallback_icns
fi

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
