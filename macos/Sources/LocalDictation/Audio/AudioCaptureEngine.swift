import AVFoundation
import CoreAudio
import Foundation

/// Captures mic audio and delivers 16kHz mono Int16 PCM chunks to `onSamples`.
/// Vosk requires exactly that format; the hardware tap format is whatever
/// AVAudioEngine reports (commonly 44.1/48kHz Float32), so every buffer goes
/// through an AVAudioConverter before it reaches the callback.
final class AudioCaptureEngine {
    private let engine = AVAudioEngine()
    private var converter: AVAudioConverter?
    private let targetFormat = AVAudioFormat(
        commonFormat: .pcmFormatInt16,
        sampleRate: 16000,
        channels: 1,
        interleaved: true
    )!

    var onSamples: (([Int16]) -> Void)?

    /// `deviceUID` is an AVCaptureDevice.uniqueID, or nil for system default.
    func start(deviceUID: String?) throws {
        if let deviceUID, !deviceUID.isEmpty {
            try selectInputDevice(uid: deviceUID)
        }

        let input = engine.inputNode
        let nativeFormat = input.inputFormat(forBus: 0)

        guard let converter = AVAudioConverter(from: nativeFormat, to: targetFormat) else {
            throw NSError(domain: "AudioCaptureEngine", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Could not build an audio converter for the current input format."
            ])
        }
        self.converter = converter

        input.installTap(onBus: 0, bufferSize: 2048, format: nativeFormat) { [weak self] buffer, _ in
            self?.convertAndEmit(buffer)
        }

        engine.prepare()
        try engine.start()
    }

    func stop() {
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        converter = nil
    }

    private func convertAndEmit(_ buffer: AVAudioPCMBuffer) {
        guard let converter else { return }

        let ratio = targetFormat.sampleRate / buffer.format.sampleRate
        let outCapacity = AVAudioFrameCount(Double(buffer.frameLength) * ratio) + 16
        guard let outBuffer = AVAudioPCMBuffer(pcmFormat: targetFormat, frameCapacity: outCapacity) else { return }

        var error: NSError?
        let status = converter.convert(to: outBuffer, error: &error) { _, outStatus in
            outStatus.pointee = .haveData
            return buffer
        }

        guard status != .error, error == nil else { return }
        guard let channelData = outBuffer.int16ChannelData else { return }

        let frameCount = Int(outBuffer.frameLength)
        guard frameCount > 0 else { return }
        let samples = Array(UnsafeBufferPointer(start: channelData[0], count: frameCount))
        onSamples?(samples)
    }

    /// Best-effort: points AVAudioEngine's input node at a specific device.
    /// On failure this just leaves the system default in place rather than
    /// throwing, since the default is an acceptable fallback for Phase 1.
    private func selectInputDevice(uid: String) throws {
        guard let audioDeviceID = AudioDeviceLookup.deviceID(forUID: uid) else { return }
        let audioUnit = engine.inputNode.audioUnit
        guard let audioUnit else { return }
        var deviceID = audioDeviceID
        AudioUnitSetProperty(
            audioUnit,
            kAudioOutputUnitProperty_CurrentDevice,
            kAudioUnitScope_Global,
            0,
            &deviceID,
            UInt32(MemoryLayout<AudioDeviceID>.size)
        )
    }
}

private enum AudioDeviceLookup {
    static func deviceID(forUID uid: String) -> AudioDeviceID? {
        var propertySize: UInt32 = 0
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyDevices,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain
        )
        guard AudioObjectGetPropertyDataSize(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &propertySize) == noErr else {
            return nil
        }
        let count = Int(propertySize) / MemoryLayout<AudioDeviceID>.size
        var deviceIDs = [AudioDeviceID](repeating: 0, count: count)
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &propertySize, &deviceIDs) == noErr else {
            return nil
        }

        for deviceID in deviceIDs {
            var uidAddress = AudioObjectPropertyAddress(
                mSelector: kAudioDevicePropertyDeviceUID,
                mScope: kAudioObjectPropertyScopeGlobal,
                mElement: kAudioObjectPropertyElementMain
            )
            var cfUID: CFString? = nil
            var size = UInt32(MemoryLayout<CFString?>.size)
            let status = AudioObjectGetPropertyData(deviceID, &uidAddress, 0, nil, &size, &cfUID)
            if status == noErr, let cfUID, cfUID as String == uid {
                return deviceID
            }
        }
        return nil
    }
}
