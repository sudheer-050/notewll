import Foundation

/// UserDefaults-backed preferences. Phase 1 scope only: hotkey, mic device,
/// start-at-login, injection mode.
final class SettingsStore {
    static let shared = SettingsStore()

    private let defaults = UserDefaults.standard

    private enum Key {
        static let hotkeyCombo = "hotkeyCombo"
        static let notesHotkeyCombo = "notesHotkeyCombo"
        static let microphoneDeviceID = "microphoneDeviceID"
        static let startAtLogin = "startAtLogin"
        static let injectionMode = "injectionMode"
    }

    var hotkeyCombo: KeyCombo {
        get {
            guard let data = defaults.data(forKey: Key.hotkeyCombo),
                  let combo = try? JSONDecoder().decode(KeyCombo.self, from: data) else {
                return .defaultDictate
            }
            return combo
        }
        set {
            if let data = try? JSONEncoder().encode(newValue) {
                defaults.set(data, forKey: Key.hotkeyCombo)
            }
        }
    }

    var notesHotkeyCombo: KeyCombo {
        get {
            guard let data = defaults.data(forKey: Key.notesHotkeyCombo),
                  let combo = try? JSONDecoder().decode(KeyCombo.self, from: data) else {
                return .defaultNotes
            }
            return combo
        }
        set {
            if let data = try? JSONEncoder().encode(newValue) {
                defaults.set(data, forKey: Key.notesHotkeyCombo)
            }
        }
    }

    var microphoneDeviceID: String {
        get { defaults.string(forKey: Key.microphoneDeviceID) ?? "" }
        set { defaults.set(newValue, forKey: Key.microphoneDeviceID) }
    }

    var startAtLogin: Bool {
        get { defaults.bool(forKey: Key.startAtLogin) }
        set { defaults.set(newValue, forKey: Key.startAtLogin) }
    }

    var injectionMode: InjectionMode {
        get {
            guard let raw = defaults.string(forKey: Key.injectionMode),
                  let mode = InjectionMode(rawValue: raw) else {
                return .paste
            }
            return mode
        }
        set { defaults.set(newValue.rawValue, forKey: Key.injectionMode) }
    }
}
