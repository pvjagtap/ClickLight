import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let settingsStore = SettingsStore()
    private lazy var settingsWindowController = SettingsWindowController(
        settingsStore: settingsStore,
        launchAtLogin: launchAtLogin,
        permissions: permissions
    )
    private lazy var overlayCoordinator = OverlayCoordinator(settingsStore: settingsStore)
    private lazy var captureController = ClickCaptureController(settingsStore: settingsStore, eventTap: eventTap)
    private lazy var statusController = StatusController(
        settingsStore: settingsStore,
        permissions: permissions,
        launchAtLogin: launchAtLogin,
        captureStatus: { [weak self] in self?.captureController.statusLabel ?? "Not Started" },
        onCheckForUpdates: { UpdateChecker.shared.checkForUpdates() },
        updatesAreConfigured: { UpdateChecker.shared.isConfigured },
        onOpenSettings: { [weak self] in self?.openSettings() },
        onTestPulse: { [weak self] in self?.showTestPulse() },
        onQuit: { NSApplication.shared.terminate(nil) }
    )
    private let eventTap = ClickEventTap()
    private let permissions = PermissionController()
    private let launchAtLogin = LaunchAtLoginController()
    private var captureEnabledState: Bool?

    func applicationDidFinishLaunching(_ notification: Notification) {
        overlayCoordinator.start()
        permissions.requestAccessibilityIfNeeded()
        captureEnabledState = settingsStore.settings.isEnabled
        captureController.startIfEnabled()
        statusController.start()

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(settingsDidChange),
            name: SettingsStore.didChangeNotification,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(clickEventDidArrive(_:)),
            name: ClickEventTap.didReceiveClickEvent,
            object: nil
        )
    }

    func applicationWillTerminate(_ notification: Notification) {
        captureController.stop()
    }

    @objc private func settingsDidChange() {
        overlayCoordinator.refreshSettings()
        let isEnabled = settingsStore.settings.isEnabled
        guard captureEnabledState != isEnabled else { return }
        captureEnabledState = isEnabled
        captureController.refreshEnabledState()
    }

    @objc private func clickEventDidArrive(_ notification: Notification) {
        guard let box = notification.object as? ClickEventBox else { return }
        overlayCoordinator.show(box.event)
    }

    private func showTestPulse() {
        overlayCoordinator.show(ClickEvent(
            kind: .leftDown,
            location: NSEvent.mouseLocation,
            timestamp: CACurrentMediaTime()
        ))
    }

    private func openSettings() {
        settingsWindowController.show()
    }
}
