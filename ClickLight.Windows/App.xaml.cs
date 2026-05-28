using System.Windows;

namespace ClickLight.Windows;

public partial class App : Application
{
    private TrayIconController? _trayIcon;
    private OverlayCoordinator? _overlayCoordinator;
    private MouseHookController? _mouseHook;
    private SettingsStore? _settingsStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsStore = new SettingsStore();
        _overlayCoordinator = new OverlayCoordinator(_settingsStore);
        _mouseHook = new MouseHookController(_settingsStore, OnClickEvent);
        _trayIcon = new TrayIconController(_settingsStore, _mouseHook, _overlayCoordinator);

        if (_settingsStore.Settings.IsEnabled)
        {
            _mouseHook.Start();
        }

        _overlayCoordinator.Start();
        _trayIcon.Show();
    }

    private void OnClickEvent(ClickEvent clickEvent)
    {
        Dispatcher.BeginInvoke(() => _overlayCoordinator?.Show(clickEvent));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Stop();
        _trayIcon?.Dispose();
        _overlayCoordinator?.Stop();
        base.OnExit(e);
    }
}
