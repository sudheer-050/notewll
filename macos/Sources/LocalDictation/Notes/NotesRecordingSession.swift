import Foundation

/// Orchestrates one note recording end-to-end: owns a dedicated
/// `AudioCaptureEngine` + `WavFileWriter` + in-memory accumulator, ticks an
/// elapsed-time callback (~1/sec) for the status item, and on stop runs the
/// noise-gated accumulator through a single `VoskRecognizer` pass to produce
/// the final transcript and auto-title. This phase deliberately simplifies
/// away the real app's live per-segment background transcription.
final class NotesRecordingSession {
    enum State {
        case idle
        case recording
    }

    enum SessionError: Error {
        case alreadyRecording
    }

    private(set) var state: State = .idle

    var onElapsedTick: ((Int) -> Void)?
    var onFinished: ((NoteDocument?) -> Void)?

    private let voskModel: VoskModel
    private let audioCapture = AudioCaptureEngine()
    private var wavWriter: WavFileWriter?
    private var accumulator: [Int16] = []
    private var currentNoteID: UUID?
    private var startedAt: Date?
    private var elapsedTimer: Timer?

    init(voskModel: VoskModel) {
        self.voskModel = voskModel
    }

    func start() throws {
        guard state == .idle else { throw SessionError.alreadyRecording }

        let id = UUID()
        currentNoteID = id
        accumulator = []

        let store = NotesStore.shared
        try FileManager.default.createDirectory(at: store.folderURL(for: id), withIntermediateDirectories: true)
        wavWriter = try WavFileWriter(url: store.wavURL(for: id))

        audioCapture.onSamples = { [weak self] samples in
            self?.handleSamples(samples)
        }

        let micID = SettingsStore.shared.microphoneDeviceID
        try audioCapture.start(deviceUID: micID.isEmpty ? nil : micID)

        startedAt = Date()
        state = .recording

        let timer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            self?.tickElapsed()
        }
        RunLoop.main.add(timer, forMode: .common)
        elapsedTimer = timer
    }

    func stop() {
        guard state == .recording, let id = currentNoteID else { return }
        state = .idle

        elapsedTimer?.invalidate()
        elapsedTimer = nil
        audioCapture.stop()
        audioCapture.onSamples = nil

        let duration = startedAt.map { Date().timeIntervalSince($0) } ?? 0
        startedAt = nil

        do {
            try wavWriter?.finalize()
        } catch {
            NSLog("Notewll: failed to finalize note WAV: \(error)")
        }
        wavWriter = nil

        let gated = QuietAudioGate.filter(accumulator)
        accumulator = []

        var transcript = ""
        do {
            let recognizer = try VoskRecognizer(model: voskModel)
            recognizer.accept(samples: gated)
            transcript = recognizer.finishAndGetText()
        } catch {
            NSLog("Notewll: failed to transcribe note: \(error)")
        }

        let segment = NoteSegment(
            source: "Microphone",
            startFrame: 0,
            frames: gated.count,
            text: transcript,
            rawText: transcript,
            complete: true,
            error: nil
        )

        var note = NoteDocument(
            id: id,
            title: NotesRecordingSession.placeholderTitle(),
            createdUtc: Date(),
            autoTitle: true,
            mode: "Microphone",
            userName: nil,
            writtenNotes: "",
            durationSeconds: duration,
            participants: [],
            segments: [segment],
            status: .completed,
            captureError: nil,
            deletedUtc: nil
        )
        NotesRecordingSession.applyAutoTitle(to: &note)

        do {
            try NotesStore.shared.save(note)
            onFinished?(note)
        } catch {
            NSLog("Notewll: failed to save note: \(error)")
            onFinished?(nil)
        }

        currentNoteID = nil
    }

    private func handleSamples(_ samples: [Int16]) {
        accumulator.append(contentsOf: samples)
        do {
            try wavWriter?.append(samples: samples)
        } catch {
            NSLog("Notewll: failed to write note audio: \(error)")
        }
    }

    private func tickElapsed() {
        guard let startedAt else { return }
        let seconds = Int(Date().timeIntervalSince(startedAt))
        onElapsedTick?(seconds)
    }

    private static func placeholderTitle() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d, HH:mm"
        return "Voice note \u{00b7} \(formatter.string(from: Date()))"
    }

    /// Mirrors the real app's `NoteMetadata.UpdateTitle`: strips a leading
    /// greeting+introduction ("Hi, my name is Alex") and leading filler
    /// words, takes the first 8 word-like tokens, capitalizes, truncates at
    /// 60 chars -- only replacing the placeholder title if >=3 words remain.
    private static func applyAutoTitle(to note: inout NoteDocument) {
        guard note.autoTitle else { return }
        var text = note.transcriptText
        guard !text.isEmpty else { return }

        text = replace(in: text, pattern: #"(?i)^(?:(?:hi|hello|hey)[,.!]?\s+)?(?:my name is|i am|i['\u{2019}]m)\s+[\p{L}\p{M}'\u{2019}-]+[,.!]?\s*"#, with: "")
        text = replace(in: text, pattern: #"(?i)^(?:um|uh|okay|so|well)[,.!]?\s+"#, with: "")
        text = text.trimmingCharacters(in: .whitespacesAndNewlines)

        let words = extractWords(from: text, limit: 8)
        guard words.count >= 3 else { return }

        var title = words.joined(separator: " ")
        title = title.prefix(1).uppercased() + title.dropFirst()
        if title.count > 60 {
            title = String(title.prefix(57)).trimmingCharacters(in: .whitespaces) + "..."
        }
        note.title = title
    }

    private static func replace(in text: String, pattern: String, with replacement: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return text }
        let range = NSRange(text.startIndex..., in: text)
        return regex.stringByReplacingMatches(in: text, range: range, withTemplate: replacement)
    }

    private static func extractWords(from text: String, limit: Int) -> [String] {
        guard let regex = try? NSRegularExpression(pattern: #"[\p{L}\p{N}][\p{L}\p{N}'\u{2019}-]*"#) else { return [] }
        let range = NSRange(text.startIndex..., in: text)
        var words: [String] = []
        regex.enumerateMatches(in: text, range: range) { match, _, stop in
            guard let match, let matchRange = Range(match.range, in: text) else { return }
            words.append(String(text[matchRange]))
            if words.count >= limit { stop.pointee = true }
        }
        return words
    }
}
