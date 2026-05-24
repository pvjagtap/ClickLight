# ClickLight

A macOS menu bar app that highlights your clicks during live demos, so viewers can see exactly when you pressed even if the UI responds slowly.

## Demo

![ClickLight showing click highlights from the macOS menu bar](docs/assets/ClickLight.gif)

## Install

Build ClickLight locally and copy it into your Applications folder:

```bash
chmod +x build-app.sh
./build-app.sh
mkdir -p "$HOME/Applications"
cp -R ClickLight.app "$HOME/Applications/ClickLight.app"
open "$HOME/Applications/ClickLight.app"
```

See [Manual Install](docs/MANUAL_INSTALL.md) for the full local install flow.

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

Start with [Local Development](docs/LOCAL_DEVELOPMENT.md).

## Uninstall

```bash
rm -rf "$HOME/Applications/ClickLight.app"
defaults delete dev.codex.ClickLight
```

## License

MIT. See [LICENSE](LICENSE).
