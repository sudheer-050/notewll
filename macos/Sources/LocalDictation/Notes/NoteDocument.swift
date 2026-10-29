import Foundation

enum NoteStatus: String, Codable {
    case recording
    case completed
    case error
}

/// One contiguous chunk of transcribed audio. The real Windows app splits a
/// note into many segments (queued/transcribed live, supports retry). This
/// phase keeps it simple: a finished note has exactly one segment spanning
/// the whole recording, transcribed in a single pass at stop time. The shape
/// is kept multi-segment anyway so a future live-transcription phase doesn't
/// need a data-model migration.
struct NoteSegment: Codable {
    var source: String = "Microphone"
    var startFrame: Int
    var frames: Int
    var text: String
    var rawText: String
    var complete: Bool
    var error: String?
}

struct NoteDocument: Codable, Identifiable {
    var id: UUID
    var title: String
    var createdUtc: Date
    var autoTitle: Bool
    var mode: String = "Microphone"
    var userName: String?
    var writtenNotes: String = ""
    var durationSeconds: Double
    /// Always empty for now -- speaker/participant detection isn't ported in
    /// this phase. Field kept so a future phase doesn't need a migration.
    var participants: [String] = []
    var segments: [NoteSegment]
    var status: NoteStatus
    var captureError: String?
    var deletedUtc: Date?

    /// Shared by the markdown renderer and the PDF exporter.
    var transcriptText: String {
        segments
            .filter { $0.complete && $0.error == nil && !$0.text.isEmpty }
            .map { $0.rawText.isEmpty ? $0.text : $0.rawText }
            .joined(separator: "\n\n")
    }
}
