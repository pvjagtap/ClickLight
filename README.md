# ClickLight

ClickLight is a tiny native macOS menu-bar app for live coding demos. It highlights mouse clicks anywhere on your Mac so viewers can see exactly when you pressed, released, right-clicked, or dragged.

It was built for the common demo problem where your physical click happens slightly before the UI changes on screen.

## Features

- Global click highlighting across macOS apps, including browsers and editors
- Separate visuals for press, release, right-click, and drag
- Menu-bar controls for size, intensity, duration, and event types
- Transparent overlay windows for each display
- Accessibility permission flow for global mouse capture
- No Xcode project required

## Requirements

- macOS 14 or newer
- Apple Swift toolchain / Command Line Tools

You can check Swift availability with:

```bash
swift --version
```

## Build And Run

From the project root:

```bash
chmod +x build-app.sh
./build-app.sh
open ClickLight.app
```

The build script compiles the Swift package in release mode and creates `ClickLight.app` in the project folder.

## Install

ClickLight does not have a packaged installer yet. To install it locally, build the app and copy it into your Applications folder:

```bash
./build-app.sh
mkdir -p "$HOME/Applications"
cp -R ClickLight.app "$HOME/Applications/ClickLight.app"
open "$HOME/Applications/ClickLight.app"
```

You can also drag `ClickLight.app` into `/Applications` in Finder.

On first launch, macOS may ask for Accessibility access because ClickLight listens for global mouse clicks. If clicks do not show highlights, open:

```text
System Settings -> Privacy & Security -> Accessibility
```

Then enable `ClickLight`, quit the app from the menu bar, and reopen it.

## How It Works

ClickLight is a native Swift/AppKit app. It uses:

- `CGEventTap` for low-level global mouse events
- `NSEvent.addGlobalMonitorForEvents` as a fallback capture path
- transparent borderless `NSWindow` overlays on each screen
- AppKit drawing for the pulse animations
- `NSStatusItem` for the menu-bar controls

For more detail on how this is built without an Xcode project, read [Building Without Xcode](docs/BUILDING_WITHOUT_XCODE.md).

For installation steps, read [Installation](docs/INSTALLATION.md).

For local development and iteration instructions, read [Local Development](docs/LOCAL_DEVELOPMENT.md).

## Project Layout

```text
.
├── Package.swift
├── Info.plist
├── build-app.sh
├── Sources/ClickLight
│   ├── AppDelegate.swift
│   ├── ClickEventTap.swift
│   ├── ClickOverlayView.swift
│   ├── ClickOverlayWindow.swift
│   ├── OverlayCoordinator.swift
│   ├── SettingsStore.swift
│   └── StatusController.swift
└── docs
    ├── BUILDING_WITHOUT_XCODE.md
    ├── INSTALLATION.md
    └── LOCAL_DEVELOPMENT.md
```

## License

ClickLight is open source under the [MIT License](LICENSE).
