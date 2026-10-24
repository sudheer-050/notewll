# Hands-free, my voice only - 0.10.0 local preview

Built 2026-10-24. Not published or installed into the running user's app by this task.

## Delivered behavior

Preferences > Hands-free provides optional dependency setup, three six-second enrollment samples, start/pause and profile deletion. An existing compatible enhanced-speech environment is reused. New setup requires Python 3.10+ and offers local CPU voice-matching package downloads. The ordinary offline Vosk engine remains independent.

Listening starts only explicitly, never at launch. A bounded 200 ms pre-roll preserves word beginnings. Phrases require at least 1.5 seconds of detected speech and end after 700 ms silence, or at the eight-second bound. Speech received during inference is discarded rather than queued without limit. After focus/input changes or backpressure, a fresh pause is required before another phrase starts.

Resemblyzer compares overlapping three-second windows with three enrollment references. Experimental acceptance thresholds are 0.86 against the centroid and 0.80 against every reference. An entire phrase is rejected if any window fails. This is a conservative filter, not a validated biometric security boundary. Playback, overlapping speech and similar voices are not reliably excluded; anything the enrolled person says while listening may be typed.

Only accepted speech is transcribed. Delivery uses existing Native.WriteLive, appends once, and checks mode revision, foreground/focus/caret identity and physical-input epoch again on the UI thread. No UI Automation, focus stealing, automatic paste, clipboard fallback or automatic Enter is added. Escape and the listening ball stop capture and invalidate pending results. Shortcut dictation and note/mic tasks pause this mode.

Native password Edit styles and Notewll's own windows are blocked. Browser password fields and programmatic focus changes within some custom controls cannot be reliably identified by this direct-typing path. Users must choose the intended text field before speaking.

Voice sample audio is memory-only. The three 256-element voice vectors are encrypted with Windows CurrentUser DPAPI and atomically replaced on successful enrollment. Failed enrollment keeps the previous profile. Profile deletion pauses capture. Voice caches contain numerical-library code, not recordings. No speaker/audio data was uploaded during verification.

## Completed checks

- 126 Python regression/setup/worker-policy tests passed; four live desktop checks were excluded.
- C# handsfree smoke passed: DPAPI save/load, tamper rejection, profile removal, target/window/input invalidation, short-phrase rejection, bounded silence and continuous speech, pre-roll/trailing-silence boundaries, whole-utterance suppression after target changes, streaming microphone without a growing history, and preserved shortcut recording behavior.
- The actual JSON-line worker initialized, enrolled three existing local test clips, accepted a matching clip and rejected silence. No actual user's persistent profile was enrolled; test profiles were isolated and deleted.
- C# client + encrypted profile + worker + Vosk integration passed, including a nonempty accepted-speech transcript. No typing was injected in that test.
- The installer was extracted into an isolated directory. Extracted app, voice worker and setup helper matched their source hashes. The extracted application passed the same voice/profile/transcription integration with its own bundled speech files.
- Optional setup PowerShell script parsed without errors. Package download/setup was not rerun because the required local environment was already present.
- Hands-free preferences preview rendered and was visually inspected: output/handsfree-preview/handsfree.png.
- First-use numerical caching was fixed to use writable app-data code-cache storage; feature extraction is warmed before the mic opens.

## Limited held-out voice exercise

Public LibriSpeech test-other clips distributed by the upstream Resemblyzer demo were downloaded for local developer checks only, not packaged. Three clips from speaker 1688 were used for enrollment. One held-out clip from 1688 passed (minimum centroid similarity 0.8723); speaker 1998 failed (0.6748); a concatenated mixed-speaker phrase failed (0.6890); silence failed. These four outcomes are functional examples, not a false-accept/reject rate, broad quality benchmark or guarantee.

Source and credit: https://github.com/resemble-ai/Resemblyzer/tree/master/audio_data/librispeech_test-other
Results: output/public-voice-results.json. No user recordings or embeddings were sent to that repository or the download host.

## Remaining desktop acceptance

Physical microphone capture in this managed session fails with MMSYSERR 1 (Undefined external error). Live enrollment, background-voice rejection in a real room, long-running capture, immediate pause during actual inference, focus transitions in real editors/browsers, and cross-app insertion need an ordinary Windows desktop trial. No end-to-end mic-to-keyboard success is claimed. Interactive installation/uninstall was not performed.

Installer: dist/Notewll-Setup-0.10.0.exe, 59,111,936 bytes, unsigned.
SHA-256: 79e83c03512a397eed8dbc1dcbda839cc9de3c8632bda8edd69153f9b8377fb2.

## Reproduce

Build with scripts/build_setup.ps1. Run LocalDictation.exe --handsfree-smoke <isolated-output-folder>. The --handsfree-worker-smoke command takes an isolated output folder, Python executable, bundled-engine root, and three 16 kHz mono 16-bit PCM WAV sample paths. It does not inject text or create a live user's voice profile. For the distribution payload, use installer --test-bundle <isolated-folder>, then run that extracted app's worker check.
