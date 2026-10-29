import AppKit
import SwiftUI

final class NotesListWindowController: NSWindowController {
    private(set) var viewModel = NotesListViewModel()

    convenience init() {
        let viewModel = NotesListViewModel()
        let view = NotesListView(viewModel: viewModel)
        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = "Notewll Notes"
        window.styleMask = [.titled, .closable]
        self.init(window: window)
        self.viewModel = viewModel
    }

    func show() {
        viewModel.reload()
        window?.center()
        window?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
