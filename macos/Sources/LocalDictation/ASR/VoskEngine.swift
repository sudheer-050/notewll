import Foundation

/// Swift wrapper around Vosk's native C API (imported via the bridging header
/// from Vendor/vosk_api.h). Vosk expects 16kHz mono signed 16-bit PCM frames;
/// `AudioCaptureEngine` is responsible for getting audio into that shape.
final class VoskEngine {
    enum EngineError: Error {
        case modelLoadFailed
        case recognizerCreateFailed
    }

    private var model: OpaquePointer?
    private var recognizer: OpaquePointer?
    private let sampleRate: Double = 16000

    init(modelPath: String) throws {
        vosk_set_log_level(-1) // silence Vosk's own stderr chatter
        guard let model = vosk_model_new(modelPath) else {
            throw EngineError.modelLoadFailed
        }
        self.model = model
        guard let recognizer = vosk_recognizer_new(model, Float(sampleRate)) else {
            throw EngineError.recognizerCreateFailed
        }
        self.recognizer = recognizer
    }

    /// Feed one chunk of 16kHz mono Int16 PCM samples.
    func accept(samples: [Int16]) {
        guard let recognizer else { return }
        samples.withUnsafeBufferPointer { buffer in
            guard let base = buffer.baseAddress else { return }
            _ = vosk_recognizer_accept_waveform_s(recognizer, base, Int32(buffer.count))
        }
    }

    /// Finalizes recognition and returns the transcript, resetting the
    /// recognizer so it's ready for the next dictation.
    func finishAndGetText() -> String {
        guard let recognizer else { return "" }
        guard let cResult = vosk_recognizer_final_result(recognizer) else { return "" }
        let json = String(cString: cResult)
        vosk_recognizer_reset(recognizer)
        return VoskEngine.extractText(fromResultJSON: json)
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
        if let recognizer { vosk_recognizer_free(recognizer) }
        if let model { vosk_model_free(model) }
    }
}
