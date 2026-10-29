import AppKit

/// Menu-bar icon + menu. The icon itself doubles as the listening-state
/// indicator for Phase 1 (a richer floating "ball" widget is Phase 2+).
final class StatusItemController {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)

    var onOpenSettings: (() -> Void)?
    var onQuit: (() -> Void)?

    init() {
        if let button = statusItem.button {
            let image = NSImage(named: "StatusBarIdle")
            image?.accessibilityDescription = "Notewll idle"
            image?.isTemplate = false
            button.image = image
        }

        let menu = NSMenu()
        menu.addItem(withTitle: "Settings…", action: #selector(handleSettings), keyEquivalent: ",")
            .target = self
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit Notewll", action: #selector(handleQuit), keyEquivalent: "q")
            .target = self
        statusItem.menu = menu
    }

    func setListening(_ isListening: Bool) {
        let image = NSImage(named: isListening ? "StatusBarListening" : "StatusBarIdle")
        image?.accessibilityDescription = isListening ? "Notewll listening" : "Notewll idle"
        image?.isTemplate = false
        statusItem.button?.image = image
    }

    @objc private func handleSettings() {
        onOpenSettings?()
    }

    @objc private func handleQuit() {
        onQuit?()
    }
}
