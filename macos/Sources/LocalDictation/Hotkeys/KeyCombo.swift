import CoreGraphics
import Foundation

/// A modifier+keycode combo, persisted via `SettingsStore`. `keyCode` is a
/// `CGKeyCode` (same space as `CGEvent`'s `.keyboardEventKeycode` field).
struct KeyCombo: Codable, Equatable {
    var keyCode: CGKeyCode
    var flags: UInt64 // raw CGEventFlags, masked to the modifier bits we care about

    static let relevantFlagsMask: UInt64 =
        CGEventFlags.maskShift.rawValue
        | CGEventFlags.maskControl.rawValue
        | CGEventFlags.maskAlternate.rawValue
        | CGEventFlags.maskCommand.rawValue

    /// Default Dictate hotkey: Right Option (Alt). Chosen because it's a single
    /// physical key with no character meaning, so it never fights with the
    /// focused app the way Cmd/Ctrl combos can, and push-and-hold feels natural.
    static let defaultDictate = KeyCombo(keyCode: 0x3D, flags: CGEventFlags.maskAlternate.rawValue)

    /// Default Notes hotkey: Ctrl+Shift+N, matching the real Windows app.
    /// Unlike Dictate, this is a toggle (see `NotesHotkeyManager`), so a
    /// multi-key combo doesn't have the same hold-ergonomics concern.
    static let defaultNotes = KeyCombo(
        keyCode: 0x2D,
        flags: CGEventFlags.maskControl.rawValue | CGEventFlags.maskShift.rawValue
    )

    var cgFlags: CGEventFlags {
        CGEventFlags(rawValue: flags & KeyCombo.relevantFlagsMask)
    }

    func matches(keyCode otherKeyCode: CGKeyCode, flags otherFlags: CGEventFlags) -> Bool {
        keyCode == otherKeyCode && (otherFlags.rawValue & KeyCombo.relevantFlagsMask) == flags
    }

    var displayString: String {
        var parts: [String] = []
        let f = cgFlags
        if f.contains(.maskControl) { parts.append("\u{2303}") }
        if f.contains(.maskAlternate) { parts.append("\u{2325}") }
        if f.contains(.maskShift) { parts.append("\u{21E7}") }
        if f.contains(.maskCommand) { parts.append("\u{2318}") }
        parts.append(KeyCombo.keyName(for: keyCode))
        return parts.joined()
    }

    /// Right/left Option, Shift, Control, Command physical key codes -- these
    /// never generate keyDown/keyUp, only `.flagsChanged`. Shared by
    /// `HotkeyManager` and `NotesHotkeyManager` so the modifier-only-vs-regular
    /// key branch isn't duplicated.
    static func isModifierKeyCode(_ code: CGKeyCode) -> Bool {
        [0x3D, 0x3A, 0x3C, 0x38, 0x3E, 0x3B, 0x36, 0x37].contains(code)
    }

    static func keyName(for keyCode: CGKeyCode) -> String {
        switch keyCode {
        case 0x3D: return "Right Option"
        case 0x3C: return "Right Shift"
        case 0x3E: return "Right Control"
        case 0x61: return "F7"
        case 0x31: return "Space"
        case 0x2D: return "N"
        default: return "Key \(keyCode)"
        }
    }
}
