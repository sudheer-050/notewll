# Notewll 0.10.0 — Speak naturally. Keep the words that matter.

Local dictation, voice notes and meeting transcripts for Windows.

## The headline: two text modes

Choose Raw text to keep recognized words unchanged, or Modified text to remove fillers and repeated starts with basic formatting. Original text remains available for review.

## Everything new in this preview

- Raw text and Modified text are the only text modes. Raw text bypasses cleanup and writing rules; Modified text applies basic cleanup.

- General repeated-prefix and abandoned-start cleanup, straight/curly apostrophe matching and two text modes.
- Find & act across conversations, optional local semantic retrieval with source playback, extractive highlights and reviewable action items.
- Timestamped live bookmarks: Ctrl+Alt+1/2/3 for Important, Question and Follow up.
- Projects, tags and person filters; clear correction-rule scopes and undo through rule removal.
- Experimental enrolled-voice hands-free mode with an app allowlist, visible state, lock/device-change pause and Escape to stop.
- Exact-text PDF preview redaction. Personal notes can be excluded; source audio is not redacted or included.
- Opt-in encrypted daily snapshots, authentication/tamper checks, restore previews and existing-ID protection.
- Managed optional-feature storage information and install/remove actions. Unknown download/RAM measurements stay labeled unknown.
- Current screenshots and an illustrative cleanup animation; the existing customizable physics ball remains.

## Downloads

- **Notewll-Setup-0.10.0.exe**: full Windows x64 installer with standard offline English speech included. Run it to install or update, preserving notes/preferences.
- **Notewll-Setup.exe**: the same installer under a convenient filename.
- **Notewll-Small-Update.zip**: compact update files and instructions for an existing installation. Not a full installer.
- **SHA256SUMS-0.10.0.txt**: checksums for those assets.


## Evidence and preview limits

The text-mode update passed 164 automated checks, with 2 skipped checks and 2 excluded live microphone checks. Original text, saved settings and installer packaging were checked. Physical microphone capture and typing into target applications still require desktop acceptance. This is an unsigned Windows preview.

[Product, demo and screenshots](https://notewll.myhai.org/) · [User guide](https://github.com/sudheer-050/notewll/blob/main/USER_GUIDE.md) · [Source](https://github.com/sudheer-050/notewll)

## macOS companion app (0.10.0, Phase 1 preview)

A first macOS build lives under `macos/`, versioned 0.10.0 to match this
release. It is a from-scratch native reimplementation (Swift/AppKit), not a
port of this repo's C# source — only the core dictate loop is covered so
far:

- One global hotkey (default: Right Option, hold to talk)
- Offline transcription via a bundled Vosk small English model (same engine
  this app uses as its zero-setup default on Windows)
- Text injection into the focused app (paste, with direct-typing fallback)
- Menu-bar icon, Settings (hotkey rebind, mic picker, start-at-login)

Not yet covered: Notes/Meeting modes, the Accurate/Enhanced ASR tiers,
hands-free + voice matching, semantic search, the floating indicator, and
most of the settings surface. Builds as an unsigned `.dmg` via GitHub
Actions (`.github/workflows/macos-build-dmg.yml`) since the build requires
macOS/Xcode and no Mac was available locally to produce it. See
`macos/README.md` for build/run/permission instructions.

---

## 0.10.0 - local hands-free preview (not yet published)

- Separate opt-in hands-free mode with local three-sample voice enrollment and conservative matching before transcription or typing.
- Always-on mic uses bounded phrase/pre-roll buffers; rejected and overflow speech are discarded. No automatic listening at launch.
- Escape, the listening ball and tray menu pause immediately; normal dictation and note/mic tasks pause this mode.
- Focus/physical input changes invalidate queued text, and a changed-target utterance is suppressed until silence. Direct append only; no focus stealing, UI Automation or paste fallback.
- Voice vectors are encrypted with Windows per-user DPAPI; enrollment audio is not saved. Optional local voice-matching setup is included; speech model/runtime remain bundled for standard use.
- Packaging, regression, encrypted storage, voice worker and limited held-out speaker checks pass. Physical microphone, recognition quality in real rooms and cross-app typing acceptance remain pending; no perfect speaker exclusion claim.

# Notewll 0.9.0 - Speak. Write. Make it yours.

Your next thought should not get stuck behind a keyboard. Notewll brings voice typing, personal notes and meeting recordings into one private Windows workspace - with a floating ball that has a personality of its own.

## One download. Ready for offline English speech.

Download **Notewll-Setup-0.9.0.exe**, open it, and install. This single file includes the Windows app, native speech runtime, English model and uninstaller. The standard installation needs no internet connection, Python, Docker, terminal commands or cloud account.

## Your voice. Your orbit.

- **Speak into your workflow.** Hold your chosen shortcut to dictate into the focused text field. Keep personal notes and meeting recordings in the same workspace.
- **A ball you can actually throw.** Drag, release, watch it bounce and roll to the bottom edge. Click to open the animated half-circle menu.
- **Make it yours.** Choose from 19 appearances: sports balls, solar-system worlds, or plain Color and Gradient. Pick any color from a live color range.
- **Choose your gravity.** Nine environments change the fall and airtime; a live bounce sample makes the difference visible.
- **Pick your scale.** Separate size controls for the ball and expanded menu. Tiles and icons grow with the menu.
- **Put your own image in orbit.** Choose an image from a local folder. It stays local and spins with the ball.
- **Keep the controls clear.** Black icons sit on round white tiles at 50% opacity. Live previews show the ball and expanded tray while you customize.

## Installation and compatibility

Windows 10/11 x64 with .NET Framework 4.8. English speech model included. A microphone is needed for microphone capture; computer-audio recording uses the Windows output device. Existing notes, recordings and preferences are preserved during upgrades.

This is an **unsigned Windows preview**. Optional enhanced recognition and meeting-voice models require their separate local setup/model downloads; they are not included in this installer. Recognition accuracy depends on the audio and model. No accuracy percentage, universal latency guarantee or signed automatic-update claim is made.

SHA256SUMS.txt accompanies the installer. Build and isolated offline-bundle checks are documented in verification/RELEASE_0.9.0.md.

---

# Floating ball customization - source update

This source update adds a click-to-open floating ball, an animated half-circle menu, black icons on 50%-opacity white round tiles, and gravity-driven throw/bounce/settling behavior. No fire or trail effects remain.

Preferences > Ball adds 19 appearances, nine gravity environments, live solid-color and gradient pickers, automatic bounce samples, separate closed/expanded percentage sizes, and local image overlays that rotate with the surface. Existing configurations retain moon/Earth/default-size settings.

Focused checks cover all 171 appearance/gravity combinations, saved preferences, custom colors, independent expanded sizes and icons, local image copying/removal/rotation, click behavior, screen-edge positioning and reduced motion. Publishing source does not publish a new installer or satisfy the separate speech-accuracy release gate described below.

# Notewll 0.8.1 - local accuracy candidate


Measured controls: no text on 21 synthetic non-speech clips and two saved empty clips. Thirteen speech replays finished with median 248 ms release latency; 12 finished within one second and one took 2,077 ms. These are warm backend replay timings, not physical microphone-to-keyboard measurements. True word error rate remains unknown without human-verified references.

117 automated checks passed; four live desktop checks were excluded. See verification/ACCURACY_0.8.1.md for evidence and remaining acceptance checks. Public release is blocked until reviewed accuracy references pass the release gate.

## What changed and improved

- Recognition: optional accurate dictation replaces the small Vosk shortcut recognizer with local CPU Parakeet. It processes speech during recording and stays warm between uses.
- Silence: neural speech detection suppresses recognition on non-speech. All 21 measured silence/noise controls returned no text, compared with four false raw outputs in the earlier recognizer controls.
- Measured recognition comparison: disagreement with saved Wispr hypotheses decreased from 15 to 10 word edits over 51 baseline words. Wispr output is not ground truth, so this is not a verified accuracy score or proof of superiority.
- Responsiveness: background recognition overlaps speaking; release timing includes outstanding work. Cancellation avoids waiting on the recognition lock.
- Consistency: Practice, file checks and recovery use the selected dictation engine. Accurate dictation and enhanced meeting notes have independent settings.
- Installation: updater checks folder write access before closing the app, backs up changed files and verifies payload hashes. Existing notes, recordings and preferences are preserved.
- Release checks: publication requires human-reviewed references and a passing accuracy gate. Default targets are at least 300 speech cases, 3,000 reference words and at most 3% word error rate.

## Known limits and availability

This is a source preview, not a published installer release. The one-second target is not met for every clip. Startup takes several seconds while the engine warms; wait for Ready. Physical microphone/shortcut typing and human-verified accuracy still require acceptance testing. Optional setup downloads model files; recognition runs locally. Basic mode remains available.

The following notes describe the historical 0.8.0 release.

# Notewll 0.8.0 - faster local dictation


- Shortcut dictation now recognizes speech every 250 ms while you speak, including when live insertion is disabled or held modifier keys delay typing.
- Silence, quick shortcut taps and empty recognition results return quietly to idle without warning popups, inserted text or clipboard changes.
- Playground preview no longer clears a computed result when an empty optional result is supplied.
- Existing notes, recordings, shortcuts and preferences are retained. Installer, application metadata and About now report 0.8.0.


Verification: 91 application, installer, speech-worker and regression checks passed; four live microphone/keyboard checks were excluded. Direct empty-result/cancellation checks passed too. The full installer embeds the exact verified 0.8.0 application. A cleanup-preview failure found during packaging was fixed and both preview checks now pass. Real microphone capture returns MMSYSERR 1 in this managed session, and keyboard injection is unavailable. Physical shortcut timing, cross-app insertion and recognition accuracy still need desktop acceptance. The installer is unsigned and the certificate-verified automatic updater remains unconfigured; install this preview manually from GitHub Releases.

See verification/STREAMING_DICTATION.md for the measured scope and remaining checks.

---

# Notewll 0.7.0

Windows x64, English-only, offline speech after initial setup download.

- PDF-only conversation export with readable scope previews and explicit participant-label wording. Original words remain read-only; external PDF copies are not tamper-proof.
- Responsive export, playback, storage and backup interfaces; separate retention and deleted-record recovery.
- Saved-record review separated from new recording controls; actual saved audio sources only, device/activity feedback and grouped actions.
- Apply/Cancel for device changes; permanent toolbar hiding and snooze respected across note actions.
- Compact Home, contextual recovery, consistent Notewll branding, improved themed tabs/dropdowns, searchable word/snippet forms and explicit correction saving.
- Standard, Chat, Email and conservative Code styles; user-controlled Practice and guided first-use checks.

Verification: 85 automated checks passed, four live checks excluded. Isolated UI checks preserve original words and preferences. See FIX_REPORT_0.7.0.md for all 38 review findings and remaining acceptance work.

The installer is unsigned because no code-signing certificate was provided. Published as an unsigned preview. Installation and live desktop acceptance have not been verified in this managed session. Real microphone/call capture, browser/terminal injection, mixed-DPI pointer behavior, Narrator and consented speech-accuracy comparison require a normal desktop session. The managed session denies those operations. No macOS/mobile/cloud sync release is included.


October 22, 2026 update: renamed the application to Notewll across the UI, setup, shortcuts, exports and documentation. Dictation uses direct typing without reading the target field or an automatic paste retry. Existing LocalDictation data and installation identifiers are retained for upgrades. Shortcut preferences are preserved. The Windows preview installer is unsigned; real cross-app insertion still needs desktop acceptance.
