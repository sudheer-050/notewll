import SwiftUI

struct OnboardingView: View {
    @State private var micStatus: PermissionStatus = PermissionsManager.shared.microphoneStatus()
    @State private var inputMonitoringStatus: PermissionStatus = PermissionsManager.shared.inputMonitoringStatus()
    @State private var accessibilityStatus: PermissionStatus = PermissionsManager.shared.accessibilityStatus()

    let onAllGranted: () -> Void

    private let pollTimer = Timer.publish(every: 1.0, on: .main, in: .common).autoconnect()

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("Notewll needs three permissions")
                .font(.title2).bold()
            Text("Everything stays on this Mac — audio is never uploaded anywhere.")
                .font(.callout)
                .foregroundStyle(.secondary)

            permissionRow(
                title: "Microphone",
                detail: "To hear what you dictate.",
                status: micStatus,
                action: { PermissionsManager.shared.requestMicrophone { _ in refresh() } }
            )
            permissionRow(
                title: "Input Monitoring",
                detail: "To detect the Dictate hotkey system-wide.",
                status: inputMonitoringStatus,
                action: {
                    PermissionsManager.shared.requestInputMonitoring()
                    PermissionsManager.shared.openSystemSettings(pane: .inputMonitoring)
                }
            )
            permissionRow(
                title: "Accessibility",
                detail: "To type the transcribed text into whatever app you're using.",
                status: accessibilityStatus,
                action: {
                    PermissionsManager.shared.requestAccessibility()
                    PermissionsManager.shared.openSystemSettings(pane: .accessibility)
                }
            )

            Spacer()
            Text("This is an unsigned, personal-use build, so macOS may ask you to re-approve these after a rebuild.")
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
        .padding(24)
        .frame(width: 440, height: 380)
        .onReceive(pollTimer) { _ in refresh() }
        .onAppear(perform: refresh)
    }

    private func refresh() {
        micStatus = PermissionsManager.shared.microphoneStatus()
        inputMonitoringStatus = PermissionsManager.shared.inputMonitoringStatus()
        accessibilityStatus = PermissionsManager.shared.accessibilityStatus()
        if micStatus == .granted, inputMonitoringStatus == .granted, accessibilityStatus == .granted {
            onAllGranted()
        }
    }

    @ViewBuilder
    private func permissionRow(title: String, detail: String, status: PermissionStatus, action: @escaping () -> Void) -> some View {
        HStack(alignment: .top) {
            Image(systemName: status == .granted ? "checkmark.circle.fill" : "circle")
                .foregroundStyle(status == .granted ? .green : .secondary)
                .font(.title3)
            VStack(alignment: .leading) {
                Text(title).bold()
                Text(detail).font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            if status != .granted {
                Button("Grant…", action: action)
            }
        }
    }
}
