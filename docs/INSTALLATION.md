# Installation

ClickLight does not currently ship with a signed installer or notarized release package.

For now, installing it means building the app locally and copying `ClickLight.app` into an Applications folder.

## Build

From the project root:

```bash
chmod +x build-app.sh
./build-app.sh
```

This creates:

```text
ClickLight.app
```

## Install For Your User

The easiest local install path is `~/Applications`:

```bash
mkdir -p "$HOME/Applications"
cp -R ClickLight.app "$HOME/Applications/ClickLight.app"
open "$HOME/Applications/ClickLight.app"
```

This does not require administrator access.

## Install For All Users

You can also drag `ClickLight.app` into `/Applications` using Finder.

From Terminal, installing into `/Applications` may require administrator privileges:

```bash
sudo cp -R ClickLight.app /Applications/ClickLight.app
open /Applications/ClickLight.app
```

## Accessibility Permission

ClickLight needs Accessibility permission to detect mouse clicks outside its own menu-bar app.

After launching ClickLight, open:

```text
System Settings -> Privacy & Security -> Accessibility
```

Enable `ClickLight`.

Then quit ClickLight from the menu-bar menu and reopen it.

## Verify It Works

Click the ClickLight menu-bar item and choose:

```text
Test Pulse at Pointer
```

If you see a pulse, the overlay is working.

If normal clicks still do not show pulses, check that Accessibility permission is enabled for the same copy of `ClickLight.app` that you launched.
