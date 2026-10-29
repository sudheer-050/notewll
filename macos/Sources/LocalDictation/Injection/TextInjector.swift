import AppKit
import CoreGraphics
import Foundation

enum InjectionMode: String, Codable {
    case paste
    case directType
}

/// Delivers transcribed text into whatever app/field currently has focus.
/// Both paths require Accessibility permission, since they post synthetic
/// keyboard events into another process.
final class TextInjector {
    func inject(_ text: String, mode: InjectionMode) {
        guard !text.isEmpty else { return }
        switch mode {
        case .paste:
            injectViaPaste(text)
        case .directType:
            injectViaUnicodeTyping(text)
        }
    }

    /// Default mode (mirrors the Windows app's "AutoPaste"): swap the
    /// clipboard, synthesize Cmd+V, then restore whatever was on the
    /// clipboard before.
    private func injectViaPaste(_ text: String) {
        let pasteboard = NSPasteboard.general
        let previousContents = pasteboard.string(forType: .string)
        let previousChangeCount = pasteboard.changeCount

        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)

        postCommandV()

        // Restore the previous clipboard shortly after, but only if nothing
        // else (including our own paste) has touched it in the meantime.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            guard pasteboard.changeCount == previousChangeCount + 1 else { return }
            pasteboard.clearContents()
            if let previousContents {
                pasteboard.setString(previousContents, forType: .string)
            }
        }
    }

    private func postCommandV() {
        let source = CGEventSource(stateID: .hidSystemState)
        let keyVDown = CGEvent(keyboardEventSource: source, virtualKey: 0x09, keyDown: true)
        let keyVUp = CGEvent(keyboardEventSource: source, virtualKey: 0x09, keyDown: false)
        keyVDown?.flags = .maskCommand
        keyVUp?.flags = .maskCommand
        keyVDown?.post(tap: .cgSessionEventTap)
        keyVUp?.post(tap: .cgSessionEventTap)
    }

    /// Fallback for fields that reject synthetic paste: type the Unicode
    /// string directly with no clipboard involved.
    private func injectViaUnicodeTyping(_ text: String) {
        let source = CGEventSource(stateID: .hidSystemState)
        guard let keyDown = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: true),
              let keyUp = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: false) else {
            return
        }
        let utf16 = Array(text.utf16)
        keyDown.keyboardSetUnicodeString(stringLength: utf16.count, unicodeString: utf16)
        keyUp.keyboardSetUnicodeString(stringLength: utf16.count, unicodeString: utf16)
        keyDown.post(tap: .cghidEventTap)
        keyUp.post(tap: .cghidEventTap)
    }
}
