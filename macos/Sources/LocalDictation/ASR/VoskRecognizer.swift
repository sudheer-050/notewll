import Foundation

/// Swift wrapper around one Vosk recognizer session (imported via the
/// bridging header from Vendor/vosk_api.h). Vosk expects 16kHz mono signed
/// 16-bit PCM frames; `AudioCaptureEngine`/callers are responsible for
/// getting audio into that shape. Each recognizer holds its own decode
/// state, so concurrent sessions (e.g. Dictate and a Note recording at the
/// same time) each need their own instance against a shared `VoskModel`.
final class VoskRecognizer {
    enum RecognizerError: Error {
        case createFailed
    }

    private let recognizer: OpaquePointer
    private let sampleRate: Double = 16000

    init(model: VoskModel) throws {
        guard let recognizer = vosk_recognizer_new(model.rawModel, Float(sampleRate)) else {
            throw RecognizerError.createFailed
        }
        self.recognizer = recognizer
    }

    /// Feed one chunk of 16kHz mono Int16 PCM samples.
    func accept(samples: [Int16]) {
        samples.withUnsafeBufferPointer { buffer in
            guard let base = buffer.baseAddress else { return }
            _ = vosk_recognizer_accept_waveform_s(recognizer, base, Int32(buffer.count))
        }
    }

    /// Finalizes recognition and returns the transcript, resetting the
    /// recognizer so it's ready to be reused for the next recording.
    func finishAndGetText() -> String {
        guard let cResult = vosk_recognizer_final_result(recognizer) else { return "" }
        let json = String(cString: cResult)
        vosk_recognizer_reset(recognizer)
        return VoskRecognizer.extractText(fromResultJSON: json)
    }

    private static func extractText(fromResultJSON json: String) -> String {
        guard let data = json.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let text = object["text"] as? String else {
            return ""
        }
        return text
    }

    deinit {
        vosk_recognizer_free(recognizer)
    }
}
