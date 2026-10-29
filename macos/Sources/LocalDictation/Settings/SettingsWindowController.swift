import AppKit
import SwiftUI

final class SettingsWindowController: NSWindowController {
    convenience init(hotkeyManager: HotkeyManager, notesHotkeyManager: NotesHotkeyManager, onSettingsChanged: @escaping () -> Void) {
        let view = SettingsView(hotkeyManager: hotkeyManager, notesHotkeyManager: notesHotkeyManager, onSettingsChanged: onSettingsChanged)
        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = "Notewll Settings"
        window.styleMask = [.titled, .closable]
        self.init(window: window)
    }

    func show() {
        window?.center()
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
