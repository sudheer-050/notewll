# Notewll certification notes

Store product: 9MTSFVDZD1C2. Windows desktop x64, English. No login, purchase, subscription or service credentials are required.

## runFullTrust explanation

Notewll is a native .NET Framework Windows Forms desktop application. Full trust is required for WinForms windows/tray controls, the bundled native Vosk recognizer, user-initiated microphone and WASAPI playback capture, global recording shortcuts, and SendInput insertion into the user's focused text field. Recording is controlled by the user. Hands-free mode must be explicitly enabled. No administrative privileges, driver, Windows service, security bypass, or inputInjectionBrokered capability is requested. Voice typing respects Windows integrity boundaries and does not type into elevated/protected applications.

The package includes the small English model and all native speech DLLs. Basic recognition is offline. Python workers/scripts for optional features ship in the package; additional runtime/model downloads are user initiated. Optional setup is not required for the primary dictation or notes workflows. Store builds route app updates to Microsoft Store rather than the direct-download installer. Startup uses the packaged StartupTask API and is disabled by default.

## Reviewer workflow

1. Install and launch Notewll from Start. Allow desktop microphone access in Windows privacy settings if necessary.
2. Leave optional enhanced recognition disabled. Open Preferences > Recording and use Test microphone and Check speech engine. Home > Try dictation provides a practice field.
3. Focus an ordinary, non-elevated Notepad text field, hold F8, say a short English sentence, and release F8. Review the result. Silence should produce no text.
4. In Notewll, create a personal note, record a short phrase, stop, and review its Original text and Modified text views. Save a written annotation and export the conversation.
5. Start a meeting with Microphone, Computer, or Both; computer capture requires actual playback on the selected output device. Stop and review it.
6. Close the hub; the tray/floating controls remain available. Use the tray Exit action to quit completely.
7. Preferences > About > Check for updates opens Microsoft Store. Preferences > App startup uses Windows' packaged startup task; Windows can override that setting.

## Required acceptance before release

- Install/launch/uninstall the signed package on a clean Windows user or VM, including offline launch.
- Test real microphone dictation and silence, F8/global shortcuts and insertion into Notepad and browser fields from the installed package.
- Test playback capture, note saving, export, and access after a Store package update.
- Test startup enable/disable and Windows' DisabledByUser handling with the installed package.
- Run Windows App Certification Kit against the package; resolve failures.
- Verify public privacy/support URLs and review third-party redistribution notices.

Packaging validation and staged speech loading do not prove these installed-package behaviors or Store certification.
