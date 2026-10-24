# Notewll 0.10.0 publication verification

Published Windows preview candidate prepared October 24, 2026.

- Current application suite: **164 passed, 2 skipped, 2 deselected**. The two excluded microphone tests require device access unavailable in the managed session. This is not microphone-to-editor acceptance.
- The installer was rebuilt from the current source. Installer payload/hash and isolated offline speech loading checks passed in the suite.
- Actual local Ollama/llama3.2 sentence-cleanup batch: eight warmed text-only requests measured **144-511 ms**, excluding microphone capture, ASR and desktop insertion. Both reported restart examples and basic grammar cases produced the intended text. The meaning validator rejected dropped words and changed protected facts. See SMART_SENTENCE_CLEANUP.md.
- Added contracted-question repetition checks cover straight, curly and mixed apostrophes, comma-separated and triple repeats, while preserving surrounding words and intentional separate questions.
- Roadmap integration checks cover tasks/bookmarks/filter persistence, original transcript preservation, source highlights, app allowlist, encrypted snapshot round-trip, tamper rejection, non-mutating restore preview and PDF scope/redaction.
- Current screenshots use synthetic demonstration records. The launch SVG/animation is a labeled illustration, not recorded product footage or a speed demonstration.
- Optional semantic-search inference/resource measurement and real hands-free/global-bookmark/microphone/keyboard acceptance remain pending. The installer is unsigned; no signed automatic update or perfect-recognition claim is made.

Release artifacts are distributed through GitHub Releases. Generated EXEs, recordings, credentials, settings, model caches and local diagnostic output are excluded from the source snapshot.
