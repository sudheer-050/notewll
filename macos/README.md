# LocalDictation (macOS)

**Version 0.10.0** — matches the Windows Notewll app's current version number.
This is the first macOS build: core dictate loop only (see "Phase 1 scope"
below). Nothing else from the Windows app has been ported yet.

A from-scratch macOS reimplementation of the core dictation loop from a
Windows app the author uses, called "Notewll"/LocalDictation. **This is not
a clone of that app's code** (it's closed-source .NET/WinForms) — it's a new
native Mac app with equivalent *behavior*: hold a hotkey, speak, release, and
the transcribed text is typed/pasted into whatever app is focused. Fully
offline, using [Vosk](https://alphacephei.com/vosk/) for speech recognition.

## Phase 1 scope

Only the core dictate loop is implemented so far:

- One global hotkey (default: **Right Option**, hold to talk)
- Offline transcription via a bundled Vosk small English model
- Text injection into the focused app (paste, or direct typing as a fallback)
- Menu-bar icon, a Settings window (hotkey rebind, mic picker, start-at-login)

**Not yet built** (see the plan history for the roadmap): Notes/Meeting
modes + export, the optional higher-accuracy ASR tiers, hands-free/voice
matching, semantic search over notes, the floating listening indicator, and
most of the original app's settings surface.

## Building

This project has no source dependency on a local Mac to *write* — but it
does need a macOS environment to *build and run*, since Xcode only runs on
macOS. If you're on Windows/Linux:

1. Push this repo to GitHub.
2. Go to **Actions → Build macOS DMG → Run workflow** (or push a `v*` tag).
3. Download the `LocalDictation-dmg` artifact once the run finishes.

If you do have a Mac: `brew install xcodegen`, run `Scripts/fetch_vosk.sh`
once to vendor the Vosk dylib/header/model, then `xcodegen generate` and
open `LocalDictation.xcodeproj` in Xcode.

## Installing the unsigned build

This is a personal-use build with no Apple Developer account behind it, so
macOS Gatekeeper will warn on first launch ("Apple could not verify this app
is free of malware"). Either:

- Control-click (right-click) the app → **Open**, or
- In Terminal: `xattr -dr com.apple.quarantine /path/to/LocalDictation.app`

## First-run checklist

On first launch you'll be walked through granting three permissions —
**Microphone**, **Input Monitoring**, and **Accessibility** — all required
(mic for audio, Input Monitoring to detect the hotkey, Accessibility to type
into other apps). Since the app is unsigned, you may occasionally need to
re-grant these after pulling a fresh build.

Once granted: open any text field, hold the Dictate hotkey, speak, release —
the words should appear within about a second.

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
