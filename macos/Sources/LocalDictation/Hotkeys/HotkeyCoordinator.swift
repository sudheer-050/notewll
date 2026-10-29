import CoreGraphics
import Foundation

/// Owns the single `GlobalEventMonitor`/CGEventTap and fans events out to every
/// subscriber (safe since the tap is `.listenOnly` -- nobody needs to "consume"
/// an event before anyone else sees it). Also centralizes the hotkey-rebind
/// "capture next combo" logic so it can swallow events process-wide while
/// active, rather than each manager racing to claim the next keypress.
final class HotkeyCoordinator {
    static let shared = HotkeyCoordinator()

    typealias Subscriber = (_ type: CGEventType, _ keyCode: CGKeyCode, _ flags: CGEventFlags) -> Void

    private var monitor: GlobalEventMonitor?
    private var subscribers: [UUID: Subscriber] = [:]
    private var captureCompletion: ((KeyCombo) -> Void)?
    private var lastFlags: CGEventFlags = []

    private init() {}

    @discardableResult
    func start() -> Bool {
        guard monitor == nil else { return true }
        let monitor = GlobalEventMonitor { [weak self] type, keyCode, flags in
            self?.dispatch(type: type, keyCode: keyCode, flags: flags)
        }
        let ok = monitor.start()
        if ok { self.monitor = monitor }
        return ok
    }

    func stop() {
        monitor?.stop()
        monitor = nil
    }

    @discardableResult
    func subscribe(_ handler: @escaping Subscriber) -> UUID {
        let token = UUID()
        subscribers[token] = handler
        return token
    }

    func unsubscribe(_ token: UUID) {
        subscribers.removeValue(forKey: token)
    }

    /// Puts the coordinator in a one-shot mode: the next key or modifier
    /// pressed becomes the new combo, reported via `completion`, and no
    /// subscriber sees that event. Used by the Settings rebind controls.
    func beginCapturingNextCombo(_ completion: @escaping (KeyCombo) -> Void) {
        captureCompletion = completion
    }

    func cancelCapture() {
        captureCompletion = nil
    }

    private func dispatch(type: CGEventType, keyCode: CGKeyCode, flags: CGEventFlags) {
        let maskedFlags = CGEventFlags(rawValue: flags.rawValue & KeyCombo.relevantFlagsMask)

        if let capture = captureCompletion {
            if type == .keyDown {
                captureCompletion = nil
                capture(KeyCombo(keyCode: keyCode, flags: maskedFlags.rawValue))
                lastFlags = flags
                return
            }
            if type == .flagsChanged, maskedFlags.rawValue != 0,
               maskedFlags.rawValue != (lastFlags.rawValue & KeyCombo.relevantFlagsMask) {
                captureCompletion = nil
                capture(KeyCombo(keyCode: keyCode, flags: maskedFlags.rawValue))
                lastFlags = flags
                return
            }
            lastFlags = flags
            return // swallow every event while capturing
        }

        lastFlags = flags
        for subscriber in subscribers.values {
            subscriber(type, keyCode, flags)
        }
    }
}
