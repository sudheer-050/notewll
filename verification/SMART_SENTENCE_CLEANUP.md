# Raw text and Modified text verification

The only writing modes are Raw text and Modified text.

- Raw text preserves the recognizer output, including leading/trailing whitespace, fillers and repeats. Dictionary replacements and writing-style formatting are bypassed.
- Modified text removes fillers and repeated starts with deterministic formatting.
- The retired sentence-editing preference is ignored and normalized off when settings are validated.
- Recognition, recovery, hands-free output and Home microphone checks share the same basic text-mode behavior.
- Existing notes and original transcripts remain preserved.

Validation: 164 automated checks passed; 2 checks skipped and 2 live microphone checks excluded. Native Home/navigation smoke and installer packaging checks passed. The actual two-mode settings preview is in assets/showcase/0.10.0/text-modes.png. Physical microphone capture and cross-app insertion still need desktop acceptance.
