import SwiftUI

struct SettingsView: View {
    @State private var hotkeyDisplay: String
    @State private var isCapturingHotkey = false
    @State private var microphones: [MicrophoneDevice] = MicrophoneDeviceStore.availableDevices()
    @State private var selectedMicrophoneID: String
    @State private var startAtLogin: Bool
    @State private var injectionMode: InjectionMode

    let hotkeyManager: HotkeyManager
    let onSettingsChanged: () -> Void

    init(hotkeyManager: HotkeyManager, onSettingsChanged: @escaping () -> Void) {
        self.hotkeyManager = hotkeyManager
        self.onSettingsChanged = onSettingsChanged
        _hotkeyDisplay = State(initialValue: SettingsStore.shared.hotkeyCombo.displayString)
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
                                SettingsStore.shared.hotkeyCombo = combo
                                hotkeyManager.combo = combo
                                hotkeyDisplay = combo.displayString
                                isCapturingHotkey = false
                            }
                        }
                    }
                }
                Text("Hold this key, speak, then release.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
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
        .frame(width: 420, height: 420)
        .padding()
    }
}
