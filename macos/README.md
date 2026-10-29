# LocalDictation (macOS)

**Version 0.10.0** — matches the Windows Notewll app's current version number.
This is the first macOS build: core dictate loop only (see "Phase 1 scope"
below). Nothing else from the Windows app has been ported yet.

A from-scratch macOS reimplementation of the core dictation loop from a
Windows app the author uses, called "Notewll"/LocalDictation. **This is not
a clone of that app's code** (it's closed-source .NET/WinForms) — it's a new
native Mac app with equivalent *behavior*: hold a hotkey, speak, release, and
the transcribed text is typed/pasted into whatever app is focused. Fully
offline, using [Vosk](https://alphacephei.com/vosk/) for speech recognition
— nothing you say ever leaves your Mac.

## Setup

1. **Download**: grab `LocalDictation-dmg` from the latest green run of
   [Build macOS DMG](../../actions/workflows/macos-build-dmg.yml) (Actions
   tab → pick a run → scroll to **Artifacts**). It downloads as a zip;
   unzip it to get `LocalDictation.dmg`.
2. **Install**: open the `.dmg`, drag `LocalDictation.app` to `Applications`
   (or anywhere you like).
3. **First launch — Gatekeeper warning**: this is an unsigned, personal-use
   build (no Apple Developer account), so macOS will warn "Apple could not
   verify this app is free of malware." Control-click (right-click) the app
   → **Open**, and confirm. (If that option doesn't appear, run
   `xattr -dr com.apple.quarantine /Applications/LocalDictation.app` in
   Terminal, then open it normally.)
4. **Grant permissions**: on first launch the app shows an onboarding
   window listing three required permissions — **Microphone**,
   **Input Monitoring**, and **Accessibility**. Click **Grant…** next to
   each; macOS will either prompt directly or open the right System
   Settings pane for you to flip the toggle. The window updates live and
   moves on once all three are granted.
   - *Microphone*: so the app can hear you.
   - *Input Monitoring*: so it can detect the hotkey system-wide.
   - *Accessibility*: so it can type the result into whatever app you're
     using.
5. You're set up. The app lives in the menu bar as a small mic icon — no
   Dock icon, no window, by design.

## How to use it

- **Dictate**: click into any text field, hold **Right Option (⌥)**
  (the default hotkey), speak, then release. The transcribed text is
  typed/pasted in within about a second.
- **Change the hotkey, microphone, or injection method**: click the
  menu-bar icon → **Settings…**. There you can:
  - Rebind the hotkey (click **Change…**, then press whatever key/combo
    you want to use instead)
  - Pick a specific microphone instead of the system default
  - Switch between **Paste** (default) and **Direct typing** — try direct
    typing if a particular app ignores the synthetic paste
  - Toggle **Start LocalDictation at login**
- **Quit**: menu-bar icon → **Quit LocalDictation**.

### Troubleshooting

- **Nothing happens when I hold the hotkey**: check System Settings →
  Privacy & Security → Input Monitoring — LocalDictation must be checked.
  If you rebuilt/re-downloaded the app, macOS sometimes revokes this and
  you'll need to re-grant it.
- **Text doesn't get typed into the app I'm using**: same idea, but for
  Accessibility instead — check it's still granted there. Some apps also
  reject synthetic paste; try switching to **Direct typing** in Settings.
- **No menu-bar icon appears at all**: the app may have failed to load its
  bundled Vosk model (check Console.app for `LocalDictation:` log lines) —
  this would mean the `.dmg` was built without the model, which would be a
  packaging bug worth reporting.

## Phase 1 scope

Only the core dictate loop is implemented so far:

- One global hotkey (default: **Right Option**, hold to talk)
- Offline transcription via a bundled Vosk small English model
- Text injection into the focused app (paste, or direct typing as a fallback)
- Menu-bar icon, a Settings window (hotkey rebind, mic picker, start-at-login)

**Not yet built**: Notes/Meeting modes + export, the optional
higher-accuracy ASR tiers, hands-free/voice matching, semantic search over
notes, the floating listening indicator, and most of the original app's
settings surface.

## Building from source (for developers)

You don't need this section to just *use* the app — it's for rebuilding it.

This project needs a macOS environment to build, since Xcode only runs on
macOS. If you're on Windows/Linux:

1. Push this repo to GitHub.
2. Go to **Actions → Build macOS DMG → Run workflow** (or push a tag
   matching `macos-v*`).
3. Download the `LocalDictation-dmg` artifact once the run finishes.

If you do have a Mac: `brew install xcodegen`, run `Scripts/fetch_vosk.sh`
once to vendor the Vosk dylib/header/model, then `xcodegen generate` and
open `LocalDictation.xcodeproj` in Xcode.

## Why these specific technical choices

- **CGEventTap, not Carbon `RegisterEventHotKey`**: hold-then-release needs
  reliable keyDown/keyUp pairing, which Carbon's hotkey API doesn't give you.
- **Vosk's native C API, linked directly**: no Python/ML runtime needed for
  the zero-setup default tier — matches how the Windows original's base
  engine works, and keeps Phase 1 free of heavy model downloads.
- **XcodeGen (`project.yml`) instead of a committed `.xcodeproj`**: the
  project is generated fresh in CI; nobody needs to hand-edit a `.pbxproj`.
- **Unsigned ad-hoc build**: no Apple Developer account is required for
  personal/local use — notarization is skippable, with the Gatekeeper
  workaround above.
