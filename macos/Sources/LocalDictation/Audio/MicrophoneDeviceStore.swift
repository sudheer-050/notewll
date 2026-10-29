import AVFoundation
import Foundation

/// Enumerates available input devices so Settings can offer a picker;
/// "system-default" (nil uniqueID) is always the first choice.
struct MicrophoneDevice: Identifiable, Equatable {
    let id: String // AVCaptureDevice.uniqueID, or "" for system default
    let name: String
}

final class MicrophoneDeviceStore {
    static func availableDevices() -> [MicrophoneDevice] {
        let discovery = AVCaptureDevice.DiscoverySession(
            deviceTypes: [.microphone, .external],
            mediaType: .audio,
            position: .unspecified
        )
        let devices = discovery.devices.map { MicrophoneDevice(id: $0.uniqueID, name: $0.localizedName) }
        return [MicrophoneDevice(id: "", name: "System Default")] + devices
    }
}
