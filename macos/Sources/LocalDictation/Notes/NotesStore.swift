import Foundation

/// Stores notes as one folder per note under `rootURL`, each containing
/// `note.json` (atomic write), `notes.md`, and `microphone.wav`. Mirrors the
/// shape of the real Windows app's `%LOCALAPPDATA%\LocalDictation\Notes\<guid>\`
/// store, mapped to `~/Library/Application Support/Notewll/Notes/<uuid>/`.
final class NotesStore {
    static let shared = NotesStore()

    let rootURL: URL
    private var deletedURL: URL { rootURL.appendingPathComponent(".deleted", isDirectory: true) }

    private let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        return encoder
    }()

    private let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()

    init(rootURL: URL? = nil) {
        if let rootURL {
            self.rootURL = rootURL
        } else {
            let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            self.rootURL = appSupport.appendingPathComponent("Notewll/Notes", isDirectory: true)
        }
        try? FileManager.default.createDirectory(at: self.rootURL, withIntermediateDirectories: true)
    }

    func folderURL(for id: UUID) -> URL {
        rootURL.appendingPathComponent(id.uuidString, isDirectory: true)
    }

    func wavURL(for id: UUID) -> URL {
        folderURL(for: id).appendingPathComponent("microphone.wav")
    }

    func save(_ note: NoteDocument) throws {
        let folder = folderURL(for: note.id)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let jsonData = try encoder.encode(note)
        try writeAtomic(jsonData, to: folder.appendingPathComponent("note.json"))
        let markdown = NotesMarkdownRenderer.render(note)
        try writeAtomic(Data(markdown.utf8), to: folder.appendingPathComponent("notes.md"))
    }

    func load(id: UUID) throws -> NoteDocument {
        let data = try Data(contentsOf: folderURL(for: id).appendingPathComponent("note.json"))
        return try decoder.decode(NoteDocument.self, from: data)
    }

    /// Non-deleted notes, newest first. Corrupt/unreadable entries are
    /// skipped rather than failing the whole listing.
    func listAll() -> [NoteDocument] {
        guard let entries = try? FileManager.default.contentsOfDirectory(
            at: rootURL, includingPropertiesForKeys: nil
        ) else {
            return []
        }
        var notes: [NoteDocument] = []
        for entry in entries {
            guard entry.lastPathComponent != ".deleted",
                  let id = UUID(uuidString: entry.lastPathComponent) else { continue }
            if let note = try? load(id: id) {
                notes.append(note)
            }
        }
        return notes.sorted { $0.createdUtc > $1.createdUtc }
    }

    /// Stamps `deletedUtc` then moves the whole note folder into `.deleted`,
    /// mirroring the Windows app's soft-delete (not an immediate hard delete).
    func softDelete(id: UUID) throws {
        var note = try load(id: id)
        note.deletedUtc = Date()
        try save(note)

        try FileManager.default.createDirectory(at: deletedURL, withIntermediateDirectories: true)
        let source = folderURL(for: id)
        let destination = deletedURL.appendingPathComponent(id.uuidString, isDirectory: true)
        if FileManager.default.fileExists(atPath: destination.path) {
            try FileManager.default.removeItem(at: destination)
        }
        try FileManager.default.moveItem(at: source, to: destination)
    }

    private func writeAtomic(_ data: Data, to url: URL) throws {
        let tempURL = url.appendingPathExtension("tmp")
        try data.write(to: tempURL)
        _ = try FileManager.default.replaceItemAt(url, withItemAt: tempURL)
    }
}
