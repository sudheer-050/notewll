import SwiftUI

struct SettingsView: View {
    @State private var hotkeyDisplay: String
    @State private var isCapturingHotkey = false
    @State private var notesHotkeyDisplay: String
    @State private var isCapturingNotesHotkey = false
    @State private var hotkeyConflictMessage: String?
    @State private var microphones: [MicrophoneDevice] = MicrophoneDeviceStore.availableDevices()
    @State private var selectedMicrophoneID: String
    @State private var startAtLogin: Bool
    @State private var injectionMode: InjectionMode

    let hotkeyManager: HotkeyManager
    let notesHotkeyManager: NotesHotkeyManager
    let onSettingsChanged: () -> Void

    init(hotkeyManager: HotkeyManager, notesHotkeyManager: NotesHotkeyManager, onSettingsChanged: @escaping () -> Void) {
        self.hotkeyManager = hotkeyManager
        self.notesHotkeyManager = notesHotkeyManager
        self.onSettingsChanged = onSettingsChanged
        _hotkeyDisplay = State(initialValue: SettingsStore.shared.hotkeyCombo.displayString)
        _notesHotkeyDisplay = State(initialValue: SettingsStore.shared.notesHotkeyCombo.displayString)
        _selectedMicrophoneID = State(initialValue: SettingsStore.shared.microphoneDeviceID)
        _startAtLogin = State(initialValue: LoginItemManager.shared.isEnabled)
        _injectionMode = State(initialValue: SettingsStore.shared.injectionMode)
    }

    var body: some View {
        Form {
            Section("Dictate Hotkey") {
                HStack {
                    Text(isCapturingHotkey ? "Press a key…" : hotkeyDisplay)
                        .frame(minWidth: 140, alignment: .leading)
                        .foregroundStyle(isCapturingHotkey ? .secondary : .primary)
                    Button(isCapturingHotkey ? "Cancel" : "Change…") {
                        if isCapturingHotkey {
                            isCapturingHotkey = false
                        } else {
                            isCapturingHotkey = true
                            hotkeyManager.beginCapturingNextCombo { combo in
                                isCapturingHotkey = false
                                if combo == notesHotkeyManager.combo {
                                    hotkeyConflictMessage = "That combo is already used by the Notes hotkey."
                                    return
                                }
                                SettingsStore.shared.hotkeyCombo = combo
                                hotkeyManager.combo = combo
                                hotkeyDisplay = combo.displayString
                            }
                        }
                    }
                }
                Text("Hold this key, speak, then release.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Notes Hotkey") {
                HStack {
                    Text(isCapturingNotesHotkey ? "Press a key…" : notesHotkeyDisplay)
                        .frame(minWidth: 140, alignment: .leading)
                        .foregroundStyle(isCapturingNotesHotkey ? .secondary : .primary)
                    Button(isCapturingNotesHotkey ? "Cancel" : "Change…") {
                        if isCapturingNotesHotkey {
                            isCapturingNotesHotkey = false
                        } else {
                            isCapturingNotesHotkey = true
                            notesHotkeyManager.beginCapturingNextCombo { combo in
                                isCapturingNotesHotkey = false
                                if combo == hotkeyManager.combo {
                                    hotkeyConflictMessage = "That combo is already used by the Dictate hotkey."
                                    return
                                }
                                SettingsStore.shared.notesHotkeyCombo = combo
                                notesHotkeyManager.combo = combo
                                notesHotkeyDisplay = combo.displayString
                            }
                        }
                    }
                }
                Text("Press once to start a note, press again to stop and save it.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                if let hotkeyConflictMessage {
                    Text(hotkeyConflictMessage)
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }

            Section("Microphone") {
                Picker("Input device", selection: $selectedMicrophoneID) {
                    ForEach(microphones) { device in
                        Text(device.name).tag(device.id)
                    }
                }
                .onChange(of: selectedMicrophoneID) { _, newValue in
                    SettingsStore.shared.microphoneDeviceID = newValue
                    onSettingsChanged()
                }
            }

            Section("Text Injection") {
                Picker("Method", selection: $injectionMode) {
                    Text("Paste (recommended)").tag(InjectionMode.paste)
                    Text("Direct typing").tag(InjectionMode.directType)
                }
                .pickerStyle(.radioGroup)
                .onChange(of: injectionMode) { _, newValue in
                    SettingsStore.shared.injectionMode = newValue
                }
            }

            Section("Startup") {
                Toggle("Start Notewll at login", isOn: $startAtLogin)
                    .onChange(of: startAtLogin) { _, newValue in
                        SettingsStore.shared.startAtLogin = newValue
                        LoginItemManager.shared.setEnabled(newValue)
                    }
            }
        }
        .formStyle(.grouped)
        .frame(width: 420, height: 520)
        .padding()
    }
}
