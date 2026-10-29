import Foundation

/// Owns a loaded Vosk model (the real cost -- reading the model files off
/// disk). Create one of these at app startup and share it across every
/// `VoskRecognizer` instance; `vosk_recognizer_new` against an already-loaded
/// model is cheap, so Dictate and Notes each get their own recognizer without
/// paying to reload the model twice.
final class VoskModel {
    enum ModelError: Error {
        case loadFailed
    }

    let rawModel: OpaquePointer

    init(modelPath: String) throws {
        vosk_set_log_level(-1) // silence Vosk's own stderr chatter
        guard let model = vosk_model_new(modelPath) else {
            throw ModelError.loadFailed
        }
        rawModel = model
    }

    deinit {
        vosk_model_free(rawModel)
    }
}
