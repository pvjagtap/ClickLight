using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace ClickLight.Windows;

/// <summary>
/// System tray icon with context menu. Equivalent to macOS StatusController.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsStore _settingsStore;
    private readonly MouseHookController _mouseHook;
    private readonly OverlayCoordinator _overlayCoordinator;
    private SettingsWindow? _settingsWindow;

    public TrayIconController(SettingsStore settingsStore, MouseHookController mouseHook, OverlayCoordinator overlayCoordinator)
    {
        _settingsStore = settingsStore;
        _mouseHook = mouseHook;
        _overlayCoordinator = overlayCoordinator;

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "ClickLight",
            Visible = false
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _settingsStore.SettingsChanged += RebuildMenu;
        RebuildMenu();
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    private void RebuildMenu()
    {
        var settings = _settingsStore.Settings;
        var menu = new ContextMenuStrip();

        // Enable/Disable toggle
        var enableItem = new ToolStripMenuItem(settings.IsEnabled ? "✓ Enabled" : "  Enabled");
        enableItem.Click += (_, _) =>
        {
            _settingsStore.Update(s => s.IsEnabled = !s.IsEnabled);
            if (_settingsStore.Settings.IsEnabled)
                _mouseHook.Start();
            else
                _mouseHook.Stop();
        };
        menu.Items.Add(enableItem);
        menu.Items.Add(new ToolStripSeparator());

        // Click type toggles
        AddToggle(menu, "Laser Pointer Mode", settings.ShowLaserPointer, s => s.ShowLaserPointer = !s.ShowLaserPointer);
        menu.Items.Add(new ToolStripSeparator());
        AddToggle(menu, "Show Press", settings.ShowPress, s => s.ShowPress = !s.ShowPress);
        AddToggle(menu, "Show Release", settings.ShowRelease, s => s.ShowRelease = !s.ShowRelease);
        AddToggle(menu, "Show Right-Click", settings.ShowRightClick, s => s.ShowRightClick = !s.ShowRightClick);
        AddToggle(menu, "Show Middle-Click", settings.ShowMiddleClick, s => s.ShowMiddleClick = !s.ShowMiddleClick);
        var showDragItem = new ToolStripMenuItem(settings.ShowDrag ? "✓ Show Drag" : "  Show Drag");
        showDragItem.Enabled = !settings.ShowLaserPointer;
        showDragItem.Click += (_, _) => _settingsStore.Update(s => s.ShowDrag = !s.ShowDrag);
        menu.Items.Add(showDragItem);
        menu.Items.Add(new ToolStripSeparator());

        // Size presets
        var sizeMenu = new ToolStripMenuItem("Size");
        foreach (var preset in ClickSettingPresets.SizePresets)
        {
            var item = new ToolStripMenuItem(preset.Title);
            item.Checked = Math.Abs(settings.Size - preset.Value) < 1;
            var val = preset.Value;
            item.Click += (_, _) => _settingsStore.Update(s => s.Size = val);
            sizeMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(sizeMenu);

        // Duration presets
        var durationMenu = new ToolStripMenuItem("Duration");
        foreach (var preset in ClickSettingPresets.DurationPresets)
        {
            var item = new ToolStripMenuItem(preset.Title);
            item.Checked = Math.Abs(settings.Duration - preset.Value) < 0.01;
            var val = preset.Value;
            item.Click += (_, _) => _settingsStore.Update(s => s.Duration = val);
            durationMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(durationMenu);

        // Intensity presets
        var intensityMenu = new ToolStripMenuItem("Intensity");
        foreach (var preset in ClickSettingPresets.IntensityPresets)
        {
            var item = new ToolStripMenuItem(preset.Title);
            item.Checked = Math.Abs(settings.Intensity - preset.Value) < 0.01;
            var val = preset.Value;
            item.Click += (_, _) => _settingsStore.Update(s => s.Intensity = val);
            intensityMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(intensityMenu);

        // Color presets
        var colorMenu = new ToolStripMenuItem("Color");
        foreach (ColorPreset preset in Enum.GetValues<ColorPreset>())
        {
            var item = new ToolStripMenuItem(preset.DisplayName());
            item.Checked = settings.ColorPreset == preset;
            var p = preset;
            item.Click += (_, _) => _settingsStore.Update(s => s.ColorPreset = p);
            colorMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(colorMenu);
        menu.Items.Add(new ToolStripSeparator());

        // Test pulse
        var testItem = new ToolStripMenuItem("Test Pulse");
        testItem.Click += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() => _overlayCoordinator.TestPulse());
        menu.Items.Add(testItem);

        // Settings
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(OpenSettings);
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        // Status
        var statusItem = new ToolStripMenuItem($"Status: {_mouseHook.StatusLabel}") { Enabled = false };
        menu.Items.Add(statusItem);

        // Quit
        var quitItem = new ToolStripMenuItem("Quit ClickLight");
        quitItem.Click += (_, _) => System.Windows.Application.Current?.Shutdown();
        menu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void AddToggle(ContextMenuStrip menu, string title, bool isOn, Action<ClickSettings> toggle)
    {
        var item = new ToolStripMenuItem(isOn ? $"✓ {title}" : $"  {title}");
        item.Click += (_, _) => _settingsStore.Update(toggle);
        menu.Items.Add(item);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsStore, _overlayCoordinator);
        _settingsWindow.Show();
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon programmatically (cursor click symbol)
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        using var pen = new Pen(System.Drawing.Color.White, 1.5f);
        g.DrawEllipse(pen, 3, 3, 10, 10);
        g.FillEllipse(System.Drawing.Brushes.White, 6, 6, 4, 4);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
