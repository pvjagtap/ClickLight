import Foundation
import ServiceManagement

@MainActor
protocol LaunchAtLoginManaging {
    var isEnabled: Bool { get }
    func setEnabled(_ enabled: Bool) throws
}

enum LaunchAtLoginState {
    static func toggledValue(currentlyEnabled: Bool) -> Bool {
        !currentlyEnabled
    }
}

@MainActor
final class LaunchAtLoginController: LaunchAtLoginManaging {
    private var cachedIsEnabled = SMAppService.mainApp.status == .enabled

    var isEnabled: Bool {
        cachedIsEnabled
    }

    func setEnabled(_ enabled: Bool) throws {
        if enabled {
            try SMAppService.mainApp.register()
        } else {
            try SMAppService.mainApp.unregister()
        }
        cachedIsEnabled = enabled
    }
}
