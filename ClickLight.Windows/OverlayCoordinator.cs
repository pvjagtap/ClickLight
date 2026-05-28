using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClickLight.Windows;

/// <summary>
/// Manages overlay windows across all monitors. Rebuilds when display configuration changes.
/// Equivalent to OverlayCoordinator on macOS.
/// </summary>
public sealed class OverlayCoordinator
{
    private readonly SettingsStore _settingsStore;
    private readonly Dictionary<string, OverlayWindow> _overlays = new();
    private readonly List<(ClickKind Kind, double X, double Y, long TickMs)> _recentEvents = new();
    private readonly object _recentLock = new();

    public OverlayCoordinator(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _settingsStore.SettingsChanged += OnSettingsChanged;
    }

    public void Start()
    {
        RebuildOverlays();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void Stop()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        foreach (var overlay in _overlays.Values)
            overlay.Close();
        _overlays.Clear();
    }

    public void Show(ClickEvent clickEvent)
    {
        var settings = _settingsStore.Settings;
        if (!settings.IsEnabled) return;
        if (!ShouldShow(clickEvent.Kind, settings)) return;
        // Skip duplicate detection for Drag and Move — continuously updated
        if (clickEvent.Kind != ClickKind.Drag && clickEvent.Kind != ClickKind.Move && !ShouldAccept(clickEvent)) return;

        var screenKey = GetScreenKey(clickEvent.X, clickEvent.Y);
        if (screenKey != null && _overlays.TryGetValue(screenKey, out var overlay))
        {
            overlay.ShowPulse(clickEvent, settings);
        }
    }

    public void TestPulse()
    {
        var pos = System.Windows.Forms.Cursor.Position;
        Show(new ClickEvent(ClickKind.LeftDown, pos.X, pos.Y, 0));
    }

    private void OnSettingsChanged()
    {
        // Clear laser visuals when laser pointer mode is turned off
        if (!_settingsStore.Settings.ShowLaserPointer)
        {
            foreach (var overlay in _overlays.Values)
                overlay.ClearLaser();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(RebuildOverlays);
    }

    private void RebuildOverlays()
    {
        foreach (var overlay in _overlays.Values)
            overlay.Close();
        _overlays.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var bounds = new Rect(
                screen.Bounds.Left,
                screen.Bounds.Top,
                screen.Bounds.Width,
                screen.Bounds.Height
            );
            var key = $"{screen.DeviceName}_{screen.Bounds}";
            var overlay = new OverlayWindow(bounds);
            overlay.Show();
            _overlays[key] = overlay;
        }
    }

    private bool ShouldShow(ClickKind kind, ClickSettings settings) => kind switch
    {
        ClickKind.LeftDown => settings.ShowPress,
        ClickKind.LeftUp => settings.ShowRelease,
        ClickKind.RightDown or ClickKind.RightUp => settings.ShowRightClick,
        ClickKind.Drag => settings.ShowDrag || settings.ShowLaserPointer,
        ClickKind.Move => settings.ShowLaserPointer,
        _ => false
    };

    private bool ShouldAccept(ClickEvent evt)
    {
        lock (_recentLock)
        {
            var nowMs = Environment.TickCount64;
            _recentEvents.RemoveAll(e => nowMs - e.TickMs > 100);

            var duplicate = _recentEvents.Any(e =>
                e.Kind == evt.Kind &&
                Math.Abs(e.X - evt.X) < 3 &&
                Math.Abs(e.Y - evt.Y) < 3);

            if (!duplicate)
                _recentEvents.Add((evt.Kind, evt.X, evt.Y, nowMs));

            return !duplicate;
        }
    }

    private string? GetScreenKey(double x, double y)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Bounds.Contains((int)x, (int)y))
                return $"{screen.DeviceName}_{screen.Bounds}";
        }
        // Fallback to primary
        var primary = Screen.PrimaryScreen;
        return primary != null ? $"{primary.DeviceName}_{primary.Bounds}" : _overlays.Keys.FirstOrDefault();
    }
}
