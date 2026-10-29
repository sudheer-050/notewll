import CoreGraphics
import Foundation

/// Push-and-hold state machine for the Dictate hotkey. Handles two distinct
/// physical-key shapes:
///  - A modifier-only key (e.g. Right Option): these never generate keyDown/
///    keyUp, only `.flagsChanged`, so "down" = the bit turns on, "up" = it
///    turns back off.
///  - A regular key held together with modifiers (e.g. Ctrl+Shift+D): "down"
///    = keyDown with the required modifiers already set, "up" = keyUp of that
///    same keyCode.
final class HotkeyManager {
    var combo: KeyCombo
    var onDictateStart: (() -> Void)?
    var onDictateStop: (() -> Void)?

    /// Non-nil while in "capture next combo" mode for the rebind UI.
    var onComboCaptured: ((KeyCombo) -> Void)?

    private var monitor: GlobalEventMonitor?
    private var isHolding = false
    private var lastFlags: CGEventFlags = []

    init(combo: KeyCombo) {
        self.combo = combo
    }

    @discardableResult
    func start() -> Bool {
        let monitor = GlobalEventMonitor { [weak self] type, keyCode, flags in
            self?.handle(type: type, keyCode: keyCode, flags: flags)
        }
        let ok = monitor.start()
        if ok { self.monitor = monitor }
        return ok
    }

    func stop() {
        monitor?.stop()
        monitor = nil
    }

    /// Puts the manager in a one-shot mode: the next key or modifier pressed
    /// becomes the new combo and is reported via `onComboCaptured`. Used by
    /// the Settings rebind control.
    func beginCapturingNextCombo(_ completion: @escaping (KeyCombo) -> Void) {
        onComboCaptured = completion
    }

    private func handle(type: CGEventType, keyCode: CGKeyCode, flags: CGEventFlags) {
        let maskedFlags = CGEventFlags(rawValue: flags.rawValue & KeyCombo.relevantFlagsMask)

        if let capture = onComboCaptured {
            if type == .keyDown {
                onComboCaptured = nil
                capture(KeyCombo(keyCode: keyCode, flags: maskedFlags.rawValue))
                return
            }
            if type == .flagsChanged, maskedFlags.rawValue != 0, maskedFlags.rawValue != lastFlags.rawValue & KeyCombo.relevantFlagsMask {
                onComboCaptured = nil
                capture(KeyCombo(keyCode: keyCode, flags: maskedFlags.rawValue))
                lastFlags = flags
                return
            }
            lastFlags = flags
            return
        }

        let comboIsModifierOnly = isModifierKeyCode(combo.keyCode)

        if comboIsModifierOnly {
            guard type == .flagsChanged, keyCode == combo.keyCode else { return }
            let nowDown = maskedFlags.rawValue == combo.flags && combo.flags != 0
            if nowDown && !isHolding {
                isHolding = true
                onDictateStart?()
            } else if !nowDown && isHolding {
                isHolding = false
                onDictateStop?()
            }
        } else {
            if type == .keyDown, combo.matches(keyCode: keyCode, flags: flags), !isHolding {
                isHolding = true
                onDictateStart?()
            } else if type == .keyUp, keyCode == combo.keyCode, isHolding {
                isHolding = false
                onDictateStop?()
            }
        }

        lastFlags = flags
    }

    private func isModifierKeyCode(_ code: CGKeyCode) -> Bool {
        // Right/left Option, Shift, Control, Command physical key codes.
        [0x3D, 0x3A, 0x3C, 0x38, 0x3E, 0x3B, 0x36, 0x37].contains(code)
    }
}
