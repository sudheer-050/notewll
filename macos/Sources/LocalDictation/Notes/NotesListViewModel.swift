import AppKit
import Foundation
import UniformTypeIdentifiers

final class NotesListViewModel: ObservableObject {
    @Published var notes: [NoteDocument] = []
    @Published var errorMessage: String?

    func reload() {
        notes = NotesStore.shared.listAll()
    }

    func delete(_ note: NoteDocument) {
        do {
            try NotesStore.shared.softDelete(id: note.id)
            reload()
        } catch {
            errorMessage = "Could not delete note: \(error.localizedDescription)"
        }
    }

    func revealInFinder(_ note: NoteDocument) {
        let folder = NotesStore.shared.folderURL(for: note.id)
        NSWorkspace.shared.activateFileViewerSelecting([folder])
    }

    func exportPDF(_ note: NoteDocument) {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = note.title.isEmpty ? "Note" : note.title
        panel.allowedContentTypes = [.pdf]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            try NotesPDFExporter.export(note, to: url)
        } catch {
            errorMessage = "Could not export PDF: \(error.localizedDescription)"
        }
    }
}
