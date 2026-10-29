import AppKit
import SwiftUI

final class OnboardingWindowController: NSWindowController {
    convenience init(onAllGranted: @escaping () -> Void) {
        let view = OnboardingView(onAllGranted: onAllGranted)
        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = "Welcome to Notewll"
        window.styleMask = [.titled, .closable]
        self.init(window: window)
    }

    func show() {
        window?.center()
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
