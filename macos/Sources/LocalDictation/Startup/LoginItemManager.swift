import Foundation
import ServiceManagement

/// Wraps SMAppService's modern (macOS 13+) login-item API. No LaunchAgent
/// plist, no Registry-Run-key-style manual bookkeeping -- the system owns
/// the state, we just read it back.
final class LoginItemManager {
    static let shared = LoginItemManager()

    var isEnabled: Bool {
        SMAppService.mainApp.status == .enabled
    }

    func setEnabled(_ enabled: Bool) {
        do {
            if enabled {
                if SMAppService.mainApp.status != .enabled {
                    try SMAppService.mainApp.register()
                }
            } else {
                if SMAppService.mainApp.status == .enabled {
                    try SMAppService.mainApp.unregister()
                }
            }
        } catch {
            NSLog("LoginItemManager: failed to set enabled=\(enabled): \(error)")
        }
    }
}
