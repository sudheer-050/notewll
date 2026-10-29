import AVFoundation
import ApplicationServices
import AppKit
import CoreGraphics
import Foundation

enum PermissionStatus {
    case granted
    case denied
    case notDetermined
}

/// Checks and requests the three TCC grants Notewll needs. None of
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
    ///
    /// `CGRequestListenEventAccess()` alone is unreliable at getting the app
    /// listed in System Settings > Privacy & Security > Input Monitoring --
    /// in practice it's *attempting to create an actual event tap* that
    /// makes macOS notice the process and add it to the list (even though
    /// the attempt itself fails until the user grants access). So we do
    /// both: call the official request API, then immediately try (and
    /// discard) a real tap as the thing that actually registers us.
    func requestInputMonitoring() {
        CGRequestListenEventAccess()
        probeEventTapForTCCRegistration()
    }

    private func probeEventTapForTCCRegistration() {
        let mask: CGEventMask = 1 << CGEventType.keyDown.rawValue
        let callback: CGEventTapCallBack = { _, _, event, _ in Unmanaged.passUnretained(event) }
        if let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .listenOnly,
            eventsOfInterest: mask,
            callback: callback,
            userInfo: nil
        ) {
            CGEvent.tapEnable(tap: tap, enable: false)
        }
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
