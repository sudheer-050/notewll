import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItemController: StatusItemController?
    private var onboardingWindow: OnboardingWindowController?
    private var settingsWindow: SettingsWindowController?

    private var hotkeyManager: HotkeyManager?
    private let audioCapture = AudioCaptureEngine()
    private var voskEngine: VoskEngine?
    private let textInjector = TextInjector()

    func applicationDidFinishLaunching(_ notification: Notification) {
        let statusItemController = StatusItemController()
        statusItemController.onOpenSettings = { [weak self] in self?.openSettings() }
        statusItemController.onQuit = { NSApp.terminate(nil) }
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
        guard voskEngine == nil else { return } // already started

        guard let modelPath = Bundle.main.path(forResource: "vosk-model-small-en-us-0.15", ofType: nil) else {
            NSLog("LocalDictation: bundled Vosk model not found -- was this built without running Scripts/fetch_vosk.sh?")
            return
        }

        do {
            voskEngine = try VoskEngine(modelPath: modelPath)
        } catch {
            NSLog("LocalDictation: failed to load Vosk model: \(error)")
            return
        }

        audioCapture.onSamples = { [weak self] samples in
            self?.voskEngine?.accept(samples: samples)
        }

        let manager = HotkeyManager(combo: SettingsStore.shared.hotkeyCombo)
        manager.onDictateStart = { [weak self] in self?.handleDictateStart() }
        manager.onDictateStop = { [weak self] in self?.handleDictateStop() }
        hotkeyManager = manager

        if !manager.start() {
            NSLog("LocalDictation: could not start the global hotkey tap -- Input Monitoring permission may be missing.")
        }
    }

    private func handleDictateStart() {
        statusItemController?.setListening(true)
        let deviceID = SettingsStore.shared.microphoneDeviceID
        do {
            try audioCapture.start(deviceUID: deviceID.isEmpty ? nil : deviceID)
        } catch {
            NSLog("LocalDictation: failed to start audio capture: \(error)")
            statusItemController?.setListening(false)
        }
    }

    private func handleDictateStop() {
        audioCapture.stop()
        statusItemController?.setListening(false)
        let text = voskEngine?.finishAndGetText() ?? ""
        textInjector.inject(text, mode: SettingsStore.shared.injectionMode)
    }

    private func openSettings() {
        guard let hotkeyManager else { return }
        let window = settingsWindow ?? SettingsWindowController(hotkeyManager: hotkeyManager) { }
        settingsWindow = window
        window.show()
    }
}
