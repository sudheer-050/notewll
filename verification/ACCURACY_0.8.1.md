# Notewll 0.8.1 local accuracy verification

## Implementation

Persistent CPU Parakeet TDT recognition runs during recording. Silero neural speech detection rejects non-speech before recognition. No separate grammar service delays shortcut completion. Formatting preserves recognized words. Explicit legacy grammar refinement rejects lexical replacements except literal fillers. Accurate dictation is independent of enhanced meeting notes. Practice, file checks and recovery use the selected dictation engine. Originals and existing notes are preserved.

## Measured results

Private benchmark artifacts are in `C:/Users/gsudh/Documents/notewll-benchmark-20261023`; recordings and transcripts must not be published.

- 21 synthetic silence/noise controls: zero text outputs.
- Two saved clips with empty Wispr output: zero text outputs.
- 13 speech clips: all produced text. Disagreement against Wispr hypotheses decreased from 15 to 10 word edits over 51 baseline words (29.4% to 19.6%). These are NOT verified accuracy or WER measurements.
- Warm real-time replay release latency, including pending inference: median 248 ms, p95 1,289.2 ms, maximum 2,077 ms; 12/13 below one second. The one-second requirement is not fully met.
- Worker startup observed about 4.3 to 9.6 seconds; the UI waits for Ready. Runtime uses CPU to avoid measured GPU cold-start stalls.

## Checks

117 tests passed, four live microphone/keyboard tests deselected; unrelated external console-visibility test excluded. Tests cover model integrity, real speech detection controls, empty-result behavior, cancellation, conservative cleanup, preference migration and independence, installer payloads, and release scoring. Physical microphone capture and desktop keyboard injection remain unverified in this managed session.

There are zero human-verified reference words. Public publishing now requires reviewed references and passing score_accuracy.py (default minimum 300 speech cases, 3,000 reference words and WER at most 3%). These are project release targets, not an industry certification. Empty-reference insertions are counted separately. No claim of parity with Wispr is supported yet.

## Local update

Use `Update Notewll accuracy.cmd` after closing any active recording, or run `scripts/setup_accuracy.ps1` for full setup. The updater probes installation write access before stopping the app, backs up changed files, verifies hashes and rolls back failures. Existing preferences and recordings are retained. Build artifacts: `dist/Notewll-Setup-0.8.1.exe` and `dist/LocalDictation-0.8.1.exe`. This candidate has not been published to GitHub or websites.
