import Foundation

/// Renders a note's companion `notes.md` file. The transcript-assembly shape
/// here is reused by `NotesPDFExporter` so both outputs stay in sync.
enum NotesMarkdownRenderer {
    static func render(_ note: NoteDocument) -> String {
        var lines: [String] = []
        lines.append("# \(note.title)")
        lines.append("")

        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        lines.append("Created: \(formatter.string(from: note.createdUtc)) · Mode: \(note.mode) · Status: \(note.status.rawValue)")
        lines.append("")

        if let userName = note.userName, !userName.isEmpty {
            lines.append("User: \(userName)")
            lines.append("")
        }
        if !note.participants.isEmpty {
            lines.append("Participants: \(note.participants.joined(separator: ", "))")
            lines.append("")
        }

        lines.append("## Notes")
        lines.append("")
        lines.append(note.writtenNotes)
        lines.append("")

        lines.append("## Transcript")
        lines.append("")
        lines.append(note.transcriptText)

        return lines.joined(separator: "\n") + "\n"
    }
}
