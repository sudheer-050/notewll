# Roadmap implementation verification - October 24, 2026

This report describes the source/build changes. The installed app has not been replaced.

| Roadmap item | Implementation and limits |
|---|---|
| Search by meaning | Find & act searches saved transcript passages and personal notes with a local MiniLM embedding model. Results open the source note, audio track and timestamp. Library project/tag/person filters constrain the corpus. Setup is an explicit download; retrieval forces offline operation. Real inference and resource measurements remain unverified here because package downloads are blocked and the pre-existing Python has incompatible TorchAudio libraries. |
| Highlights | TextRank-style passage ranking selects up to six original excerpts with timestamps. Open a result to play its source. Meetings above 400 passages use an evenly spaced sample to bound graph cost. No narrative synthesis is claimed. |
| Action items | Candidates quote source statements and suggest editable owners/date phrases. They remain unconfirmed until the user ticks Confirm and saves the review. Source text and timestamp are shown with playback. Completion is cleared for unconfirmed suggestions. |
| Live bookmarks | Important, Question and Follow up buttons and global Ctrl+Alt+1/2/3 shortcuts save recording timestamps. Bookmarks can be removed or played after recording. Keyboard repeat is suppressed. Real global shortcut recording/playback remains a desktop acceptance check. |
| Projects/tags | Edit details includes Project and Tags. Search supports project:name, tag:topic and person:name, including quoted multiword values. Works across history kinds. |
| Correction learning | Existing confirmed word/snippet editor now shows application scope and explains removal/undo of future application. Raw conversation text remains preserved. |
| Hands-free controls | Empty app allowlist blocks all targets. Allowed executable names are checked at phrase capture and delivery. Windows lock/logoff pauses listening; periodic device checks pause on default-device changes or missing named microphone. Existing visible ball/tray status and Escape remain. Real desktop lock/disconnect/insertion tests remain pending. |
| Private sharing | Conversation-only default excludes personal notes. PDF preview allows selection redaction; saved PDF uses the exact redacted preview. Original data is unchanged. UI explicitly states audio is neither included nor redacted and changing scope resets redactions. |
| Model/storage manager | Optional features installs/removes managed semantic and voice-matching runtimes. Shared speech environments are identified and retained to avoid breaking other features. Semantic package downloads, installed disk, model saved bytes, app working set and measured worker RAM are displayed separately when available; unknown measurements remain labeled unknown. Existing speech environment download/RAM totals remain unavailable. |
| Backup/recovery | Opt-in daily snapshots while the app runs; encrypted AES snapshots with HMAC authentication and Windows-account DPAPI key protection. Restore preview validates the archive and shows note titles, disk size and existing-ID skips. ZIP remains available for migration. Active recordings are skipped. Snapshots are retained until manually removed. Encrypted snapshots require the same Windows account/PC; no portability is claimed. |

## Evidence

- Build: `build/LocalDictation.exe`.
- Double-click installer: `dist/Notewll-Setup.exe`.
- Automated integration: `--roadmap-smoke <empty-output-directory>` checks annotation persistence, original transcript preservation, project filters, source highlights, app allowlist, encrypted backup round-trip, non-mutating preview, existing-ID protection, tamper rejection and private redacted PDF export.
- Visual preview: `output/roadmap-check/find-and-act.png` (synthetic meeting data).
- Automated suite: 128 passed, 2 skipped, 2 live microphone tests deselected after their managed-device access failures. Targeted installer/UI/roadmap checks also passed after packaging changes.
- Repository-wide pytest discovery includes archived release trees with duplicate test module names; use `python -m pytest tests` for the current application.
- Semantic dependency setup was attempted in `output/semantic-install` and failed because `PIP_NO_INDEX=1` / network restrictions prevented downloading packages. No model-inference result or complete RAM/download measurement is claimed.
- No installed-app update, physical microphone recording, cross-app typing or human acceptance is claimed.

## Desktop acceptance still required

1. Run the installer, configure allowed apps in Preferences > Hands-free, and verify actual typing in an allowed editor and no typing in a blocked editor.
2. Lock/unlock Windows and disconnect/reconnect the microphone; confirm listening pauses and requires a manual restart.
3. Record a meeting, create each bookmark with the app unfocused, finish recording, and jump to each saved moment.
4. Install semantic search from Storage > Optional features on a machine with package access. Search a paraphrase absent from the transcript, verify supporting passages/playback, inspect measured RAM and disk, then remove the optional runtime and confirm notes remain.
5. Review suggested actions, preview/redact a PDF and restore an encrypted snapshot through the dialogs.

### Spoken restart cleanup - October 24, 2026

With cleanup enabled outside Code mode, repeated request prefixes and clear
embedded-question restarts are now removed. Example:
`Can you list what are what updates you have done?` becomes
`Can you list what updates you have done?`.
The shared cleanup covers live/final dictation, hands-free typing and recovery.
Intentional phrases such as `had had` and `that that`, negation, names, dates and
numbers are covered by preservation checks. Ambiguous standalone questions remain
literal. This does not claim unrestricted speech-repair or physical microphone proof.
The full installer and small-update ZIP have been rebuilt; installation is still separate.

### Broader local sentence cleanup

The phrase-specific restart fixes have been replaced by a generalized basic
restart pass plus optional guarded local-model sentence editing. See
[smart cleanup verification](SMART_SENTENCE_CLEANUP.md) for actual model results,
timing boundaries and the latest 153-test suite result. Installer rebuilt;
installed-app replacement and physical desktop acceptance remain separate.
