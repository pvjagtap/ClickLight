# ClickLight for Windows

A Windows port of the macOS [ClickLight](../README.md) app — highlights your mouse clicks during live demos, screen sharing, UX reviews, and presentations.

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building)
- .NET 8 Runtime (for running published builds)

## Build & Run

```powershell
cd ClickLight.Windows
dotnet run
```

## Publish a Self-Contained Executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

This creates `publish\ClickLight.exe` — a single portable executable requiring no .NET runtime installed.

## Features

All features from the macOS version are ported:

- **Click highlights** across all Windows apps
- **Separate visuals** for press, release, right-click, and drag
- **Settings window** with sliders for size, duration, intensity, and color presets
- **System tray** with quick preset menus (size, duration, intensity, color)
- **Test pulse** for verifying overlay behavior
- **Multi-monitor** support with per-monitor overlays
- **DPI-aware** (PerMonitorV2)
- **Click-through** transparent overlays (WS_EX_TRANSPARENT)
- **Persisted settings** to `%APPDATA%\ClickLight\settings.json`

## How It Works

| Component | macOS | Windows |
|-----------|-------|---------|
| Click capture | CGEvent tap + NSEvent global monitor | Win32 low-level mouse hook (WH_MOUSE_LL) |
| Overlay window | NSWindow (borderless, transparent) | WPF Window (AllowsTransparency + WS_EX_TRANSPARENT) |
| Rendering | CoreGraphics manual drawing | WPF shape animations (Ellipse + DoubleAnimation) |
| System tray | NSStatusBar menu | WinForms NotifyIcon + ContextMenuStrip |
| Settings storage | UserDefaults | JSON file in AppData |
| Multi-monitor | NSScreen enumeration | System.Windows.Forms.Screen |

## Permissions

**No special permissions required.** Unlike macOS which requires Accessibility access, Windows low-level mouse hooks work without elevation for standard desktop apps.

## Architecture

```
ClickLight.Windows/
├── App.xaml(.cs)              # Application entry point
├── MouseHookController.cs     # WH_MOUSE_LL hook (captures all clicks)
├── OverlayCoordinator.cs      # Manages overlay windows per monitor
├── OverlayWindow.xaml(.cs)    # Transparent click-through WPF window
├── ClickEvent.cs              # Click event model
├── ClickSettings.cs           # Settings model + color presets
├── ClickSettingPresets.cs     # Size/duration/intensity presets
├── SettingsStore.cs           # JSON persistence to AppData
├── SettingsWindow.xaml(.cs)   # Settings UI with sliders
├── TrayIconController.cs      # System tray icon + context menu
└── GlobalUsings.cs            # Namespace disambiguation
```

## Uninstall

Delete the executable and optionally remove settings:

```powershell
Remove-Item "$env:APPDATA\ClickLight" -Recurse -Force
```
