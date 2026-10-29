import AppKit

/// Menu-bar icon + menu. The icon itself doubles as the listening-state
/// indicator for Phase 1 (a richer floating "ball" widget is Phase 2+).
final class StatusItemController {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)

    var onOpenSettings: (() -> Void)?
    var onQuit: (() -> Void)?

    init() {
        if let button = statusItem.button {
            button.image = NSImage(systemSymbolName: "mic", accessibilityDescription: "LocalDictation idle")
        }

        let menu = NSMenu()
        menu.addItem(withTitle: "Settings…", action: #selector(handleSettings), keyEquivalent: ",")
            .target = self
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit LocalDictation", action: #selector(handleQuit), keyEquivalent: "q")
            .target = self
        statusItem.menu = menu
    }

    func setListening(_ isListening: Bool) {
        statusItem.button?.image = NSImage(
            systemSymbolName: isListening ? "mic.fill" : "mic",
            accessibilityDescription: isListening ? "LocalDictation listening" : "LocalDictation idle"
        )
    }

    @objc private func handleSettings() {
        onOpenSettings?()
    }

    @objc private func handleQuit() {
        onQuit?()
    }
}
