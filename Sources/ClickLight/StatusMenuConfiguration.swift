import AppKit

enum StatusMenuConfiguration {
    static func apply(to menu: NSMenu) {
        menu.autoenablesItems = false
    }
}
