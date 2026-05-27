import AppKit
import SwiftUI

@MainActor
final class SettingsWindowController: NSWindowController {
    private let viewModel: ClickLightSettingsViewModel

    init(
        settingsStore: SettingsStore,
        launchAtLogin: LaunchAtLoginManaging,
        permissions: PermissionController
    ) {
        let viewModel = ClickLightSettingsViewModel(
            settingsStore: settingsStore,
            launchAtLogin: launchAtLogin,
            permissions: permissions
        )
        self.viewModel = viewModel

        let hosting = NSHostingController(rootView: ClickLightSettingsView(viewModel: viewModel))
        let window = NSWindow(contentViewController: hosting)
        window.title = "ClickLight Settings"
        window.styleMask = [.titled, .closable, .miniaturizable, .resizable]
        window.setContentSize(NSSize(width: 760, height: 520))
        window.minSize = NSSize(width: 700, height: 480)
        window.center()
        window.isReleasedWhenClosed = false

        super.init(window: window)
    }

    required init?(coder: NSCoder) {
        nil
    }

    func show() {
        guard let window else { return }
        NSApp.activate(ignoringOtherApps: true)
        showWindow(nil)
        window.makeKeyAndOrderFront(nil)
        window.orderFrontRegardless()
        viewModel.refreshSystemState()
    }
}

@MainActor
final class ClickLightSettingsViewModel: NSObject, ObservableObject {
    private let settingsStore: SettingsStore
    private let launchAtLogin: LaunchAtLoginManaging
    private let permissions: PermissionController

    @Published private(set) var launchAtLoginEnabled: Bool = false
    @Published private(set) var accessibilityTrusted: Bool = false
    @Published var launchAtLoginErrorMessage: String?

    init(
        settingsStore: SettingsStore,
        launchAtLogin: LaunchAtLoginManaging,
        permissions: PermissionController
    ) {
        self.settingsStore = settingsStore
        self.launchAtLogin = launchAtLogin
        self.permissions = permissions
        super.init()
        self.launchAtLoginEnabled = launchAtLogin.isEnabled
        self.accessibilityTrusted = permissions.isAccessibilityTrusted
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(settingsDidChange),
            name: SettingsStore.didChangeNotification,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(appBecameActive),
            name: NSApplication.didBecomeActiveNotification,
            object: nil
        )
    }

    deinit {
        NotificationCenter.default.removeObserver(self)
    }

    @objc private func settingsDidChange() {
        objectWillChange.send()
    }

    @objc private func appBecameActive() {
        refreshSystemState()
    }

    func refreshSystemState() {
        launchAtLoginEnabled = launchAtLogin.isEnabled
        accessibilityTrusted = permissions.isAccessibilityTrusted
    }

    func setLaunchAtLogin(_ enabled: Bool) {
        do {
            try launchAtLogin.setEnabled(enabled)
            launchAtLoginErrorMessage = nil
        } catch {
            launchAtLoginErrorMessage = error.localizedDescription
        }
        launchAtLoginEnabled = launchAtLogin.isEnabled
    }

    func openAccessibilitySettings() {
        permissions.requestAccessibilityIfNeeded()
        permissions.openPrivacySettings()
    }

    var settings: ClickSettings {
        settingsStore.settings
    }

    var sizePresetSelection: String {
        guard let preset = ClickSettingOptions.matchingPreset(for: settings.size, in: ClickSettingOptions.sizePresets) else {
            return "custom"
        }
        return String(preset.value)
    }

    var intensityPresetSelection: String {
        guard let preset = ClickSettingOptions.matchingPreset(for: settings.intensity, in: ClickSettingOptions.intensityPresets) else {
            return "custom"
        }
        return String(preset.value)
    }

    var durationPresetSelection: String {
        guard let preset = ClickSettingOptions.matchingPreset(for: settings.duration, in: ClickSettingOptions.durationPresets) else {
            return "custom"
        }
        return String(preset.value)
    }

    func update(_ mutate: (inout ClickSettings) -> Void) {
        settingsStore.update(mutate)
    }

    func applySizePresetSelection(_ selection: String) {
        guard let value = Double(selection) else { return }
        settingsStore.update { $0.size = CGFloat(value) }
    }

    func applyIntensityPresetSelection(_ selection: String) {
        guard let value = Double(selection) else { return }
        settingsStore.update { $0.intensity = CGFloat(value) }
    }

    func applyDurationPresetSelection(_ selection: String) {
        guard let value = Double(selection) else { return }
        settingsStore.update { $0.duration = value }
    }

    func applyCustomColor(_ color: NSColor) {
        guard let rgb = color.usingColorSpace(.deviceRGB) else { return }
        settingsStore.update {
            $0.customColorRed = rgb.redComponent
            $0.customColorGreen = rgb.greenComponent
            $0.customColorBlue = rgb.blueComponent
            $0.colorPreset = .custom
        }
    }

    func resetToDefaults() {
        settingsStore.settings = .defaults
    }
}
