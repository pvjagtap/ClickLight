# ClickLight

ClickLight is a macOS menu bar app that highlights your clicks during live demos, so viewers can see exactly when you pressed even if the UI responds slowly.

## Demo

![ClickLight showing click highlights from the macOS menu bar](docs/assets/ClickLight.gif)

## Install

ClickLight does not have a packaged installer yet. Build it locally and copy it into your Applications folder:

```bash
chmod +x build-app.sh
./build-app.sh
mkdir -p "$HOME/Applications"
cp -R ClickLight.app "$HOME/Applications/ClickLight.app"
open "$HOME/Applications/ClickLight.app"
```

More detail: [Installation](docs/INSTALLATION.md)

## Features

- Click highlights across macOS apps
- Separate visuals for press, release, right-click, and drag
- Menu-bar controls for size, duration, and intensity
- Test pulse for verifying overlay behavior
- Native Swift/AppKit app
- No Xcode project required

## Permissions

ClickLight requires Accessibility permission to detect clicks outside its own menu-bar app. You will be prompted on first launch, or grant it manually in:

**System Settings -> Privacy & Security -> Accessibility**

After enabling permission, quit ClickLight from the menu bar and reopen it.

## Modify It

ClickLight is personal software: one small presentation annoyance, fixed directly. The project is intentionally small so you or an agent can change it without much ceremony.

[Local Development](docs/LOCAL_DEVELOPMENT.md)

## How It Is Built

ClickLight is a Swift Package Manager executable wrapped into a macOS `.app` bundle by `build-app.sh`.

[Building Without Xcode](docs/BUILDING_WITHOUT_XCODE.md)

## Uninstall

```bash
rm -rf "$HOME/Applications/ClickLight.app"
defaults delete dev.codex.ClickLight
```

## License

MIT. See [LICENSE](LICENSE).
