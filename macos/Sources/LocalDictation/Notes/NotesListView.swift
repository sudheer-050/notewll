import SwiftUI

struct NotesListView: View {
    @ObservedObject var viewModel: NotesListViewModel

    private static let dateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter
    }()

    var body: some View {
        Group {
            if viewModel.notes.isEmpty {
                VStack {
                    Spacer()
                    Text("No notes yet. Press the Notes hotkey to record one.")
                        .foregroundStyle(.secondary)
                    Spacer()
                }
            } else {
                List(viewModel.notes) { note in
                    row(for: note)
                }
                .listStyle(.inset)
            }
        }
        .frame(width: 480, height: 420)
        .alert("Error", isPresented: Binding(
            get: { viewModel.errorMessage != nil },
            set: { if !$0 { viewModel.errorMessage = nil } }
        )) {
            Button("OK", role: .cancel) { viewModel.errorMessage = nil }
        } message: {
            Text(viewModel.errorMessage ?? "")
        }
    }

    @ViewBuilder
    private func row(for note: NoteDocument) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(note.title).font(.headline)
                Text("\(Self.dateFormatter.string(from: note.createdUtc)) \u{00b7} \(formattedDuration(note.durationSeconds))")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Menu {
                Button("Reveal in Finder") { viewModel.revealInFinder(note) }
                Button("Export as PDF\u{2026}") { viewModel.exportPDF(note) }
                Divider()
                Button("Delete", role: .destructive) { viewModel.delete(note) }
            } label: {
                Image(systemName: "ellipsis.circle")
            }
            .menuStyle(.borderlessButton)
            .frame(width: 28)
        }
        .padding(.vertical, 4)
    }

    private func formattedDuration(_ seconds: Double) -> String {
        let total = Int(seconds.rounded())
        return String(format: "%d:%02d", total / 60, total % 60)
    }
}
