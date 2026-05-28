# ClickLight for Windows

Windows port of [ClickLight](https://github.com/aurorascharff/ClickLight) by [@aurorascharff](https://github.com/aurorascharff). Highlights your clicks during live demos, screen sharing, and presentations.

## Install

Download `ClickLight-Windows-x64.zip` from [Releases](https://github.com/pvjagtap/ClickLight/releases), extract, and run. No installation needed.

## Build from Source

```powershell
cd ClickLight.Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Features

- Click highlights across all Windows apps
- Separate visuals for press, release, right-click, and drag rectangle
- Settings window with sliders for size, duration, intensity, and color
- System tray with quick presets
- Custom color picker
- Launch at Login
- Multi-monitor + per-monitor DPI support
- Single-file standalone exe (no .NET runtime needed)

## Credits

Original macOS app by [Aurora Scharff](https://github.com/aurorascharff/ClickLight). This is an independent Windows port.
