import CoreGraphics
import Foundation

/// Toggle state machine for the Notes hotkey: fires `onToggle` once per
/// physical key-press (the caller decides start-vs-stop from its own
/// recording state), never on release. This matches the real Windows app's
/// Notes binding, which only reacts to the press edge -- unlike Dictate,
/// which is a hold-and-release gesture (`HotkeyManager`).
///
/// Debounces CGEventTap's key-repeat: holding the combo down must fire
/// `onToggle` exactly once, not repeatedly.
final class NotesHotkeyManager {
    var combo: KeyCombo
    var onToggle: (() -> Void)?

    private var subscriptionToken: UUID?
    private var isPhysicallyDown = false

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

    func beginCapturingNextCombo(_ completion: @escaping (KeyCombo) -> Void) {
        HotkeyCoordinator.shared.beginCapturingNextCombo(completion)
    }

    private func handle(type: CGEventType, keyCode: CGKeyCode, flags: CGEventFlags) {
        let maskedFlags = CGEventFlags(rawValue: flags.rawValue & KeyCombo.relevantFlagsMask)
        let comboIsModifierOnly = KeyCombo.isModifierKeyCode(combo.keyCode)

        if comboIsModifierOnly {
            guard type == .flagsChanged, keyCode == combo.keyCode else { return }
            let nowDown = maskedFlags.rawValue == combo.flags && combo.flags != 0
            if nowDown && !isPhysicallyDown {
                isPhysicallyDown = true
                onToggle?()
            } else if !nowDown {
                isPhysicallyDown = false
            }
        } else {
            if type == .keyDown, combo.matches(keyCode: keyCode, flags: flags), !isPhysicallyDown {
                isPhysicallyDown = true
                onToggle?()
            } else if type == .keyUp, keyCode == combo.keyCode {
                isPhysicallyDown = false
            }
        }
    }
}
