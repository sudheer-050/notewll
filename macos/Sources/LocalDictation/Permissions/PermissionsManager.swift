import AVFoundation
import ApplicationServices
import AppKit
import Foundation

enum PermissionStatus {
    case granted
    case denied
    case notDetermined
}

/// Checks and requests the three TCC grants LocalDictation needs. None of
/// these can be silently skipped: without Microphone there's no audio, without
/// Input Monitoring the global hotkey tap can't receive events, without
/// Accessibility synthetic keystrokes get dropped by the OS.
final class PermissionsManager {
    static let shared = PermissionsManager()

    func microphoneStatus() -> PermissionStatus {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return .granted
        case .denied, .restricted: return .denied
        case .notDetermined: return .notDetermined
        @unknown default: return .notDetermined
        }
    }

    func requestMicrophone(completion: @escaping (Bool) -> Void) {
        AVCaptureDevice.requestAccess(for: .audio) { granted in
            DispatchQueue.main.async { completion(granted) }
        }
    }

    func inputMonitoringStatus() -> PermissionStatus {
        CGPreflightListenEventAccess() ? .granted : .notDetermined
    }

    /// Triggers the system prompt if not yet decided. Returns immediately;
    /// poll `inputMonitoringStatus()` to see when the user grants it.
    func requestInputMonitoring() {
        CGRequestListenEventAccess()
    }

    func accessibilityStatus() -> PermissionStatus {
        AXIsProcessTrusted() ? .granted : .notDetermined
    }

    func requestAccessibility() {
        let options: NSDictionary = [kAXTrustedCheckOptionPrompt.takeRetainedValue() as String: true]
        AXIsProcessTrustedWithOptions(options)
    }

    func allGranted() -> Bool {
        microphoneStatus() == .granted
        && inputMonitoringStatus() == .granted
        && accessibilityStatus() == .granted
    }

    func openSystemSettings(pane: SystemSettingsPane) {
        if let url = URL(string: pane.rawValue) {
            NSWorkspace.shared.open(url)
        }
    }

    enum SystemSettingsPane: String {
        case microphone = "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone"
        case inputMonitoring = "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent"
        case accessibility = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
    }
}
