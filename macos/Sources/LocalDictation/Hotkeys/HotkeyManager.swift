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
///
/// Events are delivered by the shared `HotkeyCoordinator`, not a private
/// CGEventTap -- `NotesHotkeyManager` subscribes to the same coordinator so
/// both hotkeys work off one tap.
final class HotkeyManager {
    var combo: KeyCombo
    var onDictateStart: (() -> Void)?
    var onDictateStop: (() -> Void)?

    private var subscriptionToken: UUID?
    private var isHolding = false

    init(combo: KeyCombo) {
        self.combo = combo
    }

    func start() {
        guard subscriptionToken == nil else { return }
        subscriptionToken = HotkeyCoordinator.shared.subscribe { [weak self] type, keyCode, flags in
            self?.handle(type: type, keyCode: keyCode, flags: flags)
        }
    }

    func stop() {
        if let token = subscriptionToken {
            HotkeyCoordinator.shared.unsubscribe(token)
        }
        subscriptionToken = nil
    }

    /// Forwards to the shared coordinator -- see its doc comment. Kept as a
    /// method here so existing call sites (`hotkeyManager.beginCapturingNextCombo`)
    /// don't need to change shape.
    func beginCapturingNextCombo(_ completion: @escaping (KeyCombo) -> Void) {
        HotkeyCoordinator.shared.beginCapturingNextCombo(completion)
    }

    private func handle(type: CGEventType, keyCode: CGKeyCode, flags: CGEventFlags) {
        let maskedFlags = CGEventFlags(rawValue: flags.rawValue & KeyCombo.relevantFlagsMask)
        let comboIsModifierOnly = KeyCombo.isModifierKeyCode(combo.keyCode)

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
    }
}
