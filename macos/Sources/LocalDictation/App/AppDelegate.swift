import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItemController: StatusItemController?
    private var onboardingWindow: OnboardingWindowController?
    private var settingsWindow: SettingsWindowController?
    private var notesListWindow: NotesListWindowController?

    private var hotkeyManager: HotkeyManager?
    private var notesHotkeyManager: NotesHotkeyManager?
    private let audioCapture = AudioCaptureEngine()
    private var voskModel: VoskModel?
    private var voskRecognizer: VoskRecognizer?
    private var notesRecordingSession: NotesRecordingSession?
    private let textInjector = TextInjector()

    func applicationDidFinishLaunching(_ notification: Notification) {
        let statusItemController = StatusItemController()
        statusItemController.onOpenSettings = { [weak self] in self?.openSettings() }
        statusItemController.onQuit = { NSApp.terminate(nil) }
        statusItemController.onToggleNote = { [weak self] in self?.handleNotesToggle() }
        statusItemController.onOpenNotesList = { [weak self] in self?.openNotesList() }
        self.statusItemController = statusItemController

        if PermissionsManager.shared.allGranted() {
            startCore()
        } else {
            showOnboarding()
        }
    }

    private func showOnboarding() {
        let onboarding = OnboardingWindowController { [weak self] in
            self?.onboardingWindow?.window?.close()
            self?.onboardingWindow = nil
            self?.startCore()
        }
        onboardingWindow = onboarding
        onboarding.show()
    }

    private func startCore() {
        guard voskModel == nil else { return } // already started

        guard let modelPath = Bundle.main.path(forResource: "vosk-model-small-en-us-0.15", ofType: nil) else {
            NSLog("Notewll: bundled Vosk model not found -- was this built without running Scripts/fetch_vosk.sh?")
            return
        }

        let model: VoskModel
        do {
            model = try VoskModel(modelPath: modelPath)
            voskModel = model
        } catch {
            NSLog("Notewll: failed to load Vosk model: \(error)")
            return
        }

        do {
            voskRecognizer = try VoskRecognizer(model: model)
        } catch {
            NSLog("Notewll: failed to create the Dictate recognizer: \(error)")
            return
        }

        audioCapture.onSamples = { [weak self] samples in
            self?.voskRecognizer?.accept(samples: samples)
        }

        if !HotkeyCoordinator.shared.start() {
            NSLog("Notewll: could not start the global hotkey tap -- Input Monitoring permission may be missing.")
        }

        let manager = HotkeyManager(combo: SettingsStore.shared.hotkeyCombo)
        manager.onDictateStart = { [weak self] in self?.handleDictateStart() }
        manager.onDictateStop = { [weak self] in self?.handleDictateStop() }
        manager.start()
        hotkeyManager = manager

        let notesManager = NotesHotkeyManager(combo: SettingsStore.shared.notesHotkeyCombo)
        notesManager.onToggle = { [weak self] in self?.handleNotesToggle() }
        notesManager.start()
        notesHotkeyManager = notesManager

        let session = NotesRecordingSession(voskModel: model)
        session.onElapsedTick = { [weak self] seconds in
            self?.statusItemController?.setNotesElapsedTitle(seconds)
        }
        session.onFinished = { [weak self] _ in
            self?.statusItemController?.setNoteRecording(false)
            self?.statusItemController?.setNotesElapsedTitle(nil)
        }
        notesRecordingSession = session
    }

    private func handleDictateStart() {
        statusItemController?.setListening(true)
        let deviceID = SettingsStore.shared.microphoneDeviceID
        do {
            try audioCapture.start(deviceUID: deviceID.isEmpty ? nil : deviceID)
        } catch {
            NSLog("Notewll: failed to start audio capture: \(error)")
            statusItemController?.setListening(false)
        }
    }

    private func handleDictateStop() {
        audioCapture.stop()
        statusItemController?.setListening(false)
        let text = voskRecognizer?.finishAndGetText() ?? ""
        textInjector.inject(text, mode: SettingsStore.shared.injectionMode)
    }

    private func handleNotesToggle() {
        guard let session = notesRecordingSession else { return }
        if session.state == .recording {
            session.stop()
        } else {
            do {
                try session.start()
                statusItemController?.setNoteRecording(true)
            } catch {
                NSLog("Notewll: failed to start note recording: \(error)")
            }
        }
    }

    private func openSettings() {
        guard let hotkeyManager, let notesHotkeyManager else { return }
        let window = settingsWindow ?? SettingsWindowController(
            hotkeyManager: hotkeyManager,
            notesHotkeyManager: notesHotkeyManager
        ) { }
        settingsWindow = window
        window.show()
    }

    private func openNotesList() {
        let window = notesListWindow ?? NotesListWindowController()
        notesListWindow = window
        window.show()
    }
}
