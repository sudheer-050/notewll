import AppKit

/// Menu-bar icon + menu. The icon itself doubles as the Dictate listening
/// indicator; the button's text title (independent of the icon) shows a
/// live elapsed time while a note is recording -- a lightweight stand-in
/// for the real Windows app's floating dock/ball, which is Phase 2+.
final class StatusItemController {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let noteMenuItem = NSMenuItem()

    var onOpenSettings: (() -> Void)?
    var onQuit: (() -> Void)?
    var onToggleNote: (() -> Void)?
    var onOpenNotesList: (() -> Void)?

    init() {
        if let button = statusItem.button {
            let image = NSImage(named: "StatusBarIdle")
            image?.accessibilityDescription = "Notewll idle"
            image?.isTemplate = false
            button.image = image
            button.imagePosition = .imageLeading
        }

        noteMenuItem.title = "Start Note"
        noteMenuItem.action = #selector(handleToggleNote)
        noteMenuItem.target = self
        noteMenuItem.keyEquivalent = ""

        let menu = NSMenu()
        menu.addItem(noteMenuItem)
        menu.addItem(withTitle: "Notes…", action: #selector(handleOpenNotesList), keyEquivalent: "")
            .target = self
        menu.addItem(.separator())
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

    func setNoteRecording(_ isRecording: Bool) {
        noteMenuItem.title = isRecording ? "Stop Note" : "Start Note"
        if !isRecording {
            statusItem.button?.title = ""
        }
    }

    /// `nil` clears the elapsed-time title; otherwise shows it as "mm:ss"
    /// next to the status item's icon.
    func setNotesElapsedTitle(_ seconds: Int?) {
        guard let seconds else {
            statusItem.button?.title = ""
            return
        }
        statusItem.button?.title = String(format: " %d:%02d", seconds / 60, seconds % 60)
    }

    @objc private func handleSettings() {
        onOpenSettings?()
    }

    @objc private func handleQuit() {
        onQuit?()
    }

    @objc private func handleToggleNote() {
        onToggleNote?()
    }

    @objc private func handleOpenNotesList() {
        onOpenNotesList?()
    }
}
