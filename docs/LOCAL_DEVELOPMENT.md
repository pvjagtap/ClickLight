# Local Development

This guide explains how to build, run, and iterate on ClickLight locally.

## Prerequisites

Install Apple Command Line Tools if Swift is not already available:

```bash
xcode-select --install
```

Verify Swift:

```bash
swift --version
```

## Build The App

From the project root:

```bash
chmod +x build-app.sh
./build-app.sh
```

This creates:

```text
ClickLight.app
```

## Run The App

Open the app with:

```bash
open ClickLight.app
```

ClickLight appears in the macOS menu bar.

For a more permanent local install, copy the built app into `~/Applications`:

```bash
mkdir -p "$HOME/Applications"
cp -R ClickLight.app "$HOME/Applications/ClickLight.app"
open "$HOME/Applications/ClickLight.app"
```

## Grant Accessibility Permission

ClickLight listens for global mouse events, so macOS requires Accessibility access.

Open:

```text
System Settings -> Privacy & Security -> Accessibility
```

Enable `ClickLight`.

If click highlights still do not appear:

1. Quit ClickLight from the menu-bar menu.
2. Reopen it with `open ClickLight.app`.
3. Use `Test Pulse at Pointer` from the menu to verify the overlay is drawing.

## Iterate On The Code

After changing Swift files:

```bash
./build-app.sh
pkill -x ClickLight
open ClickLight.app
```

If `pkill` does not find a running app, that is fine. It just means ClickLight was not already open.

## Useful Files

- `Sources/ClickLight/ClickEventTap.swift`: global mouse capture
- `Sources/ClickLight/OverlayCoordinator.swift`: one overlay per screen
- `Sources/ClickLight/ClickOverlayView.swift`: pulse drawing and animation
- `Sources/ClickLight/StatusController.swift`: menu-bar UI
- `Sources/ClickLight/SettingsStore.swift`: saved preferences
- `Info.plist`: app bundle metadata
- `build-app.sh`: command-line app bundle builder

## Debugging Tips

Run the binary directly if the app does not appear in the menu bar:

```bash
ClickLight.app/Contents/MacOS/ClickLight
```

If it crashes, Terminal will show the error.

Check whether it is running:

```bash
pgrep -fl ClickLight
```

Read saved preferences:

```bash
defaults read dev.codex.ClickLight
```

Reset saved preferences:

```bash
defaults delete dev.codex.ClickLight
```

Then reopen the app.
