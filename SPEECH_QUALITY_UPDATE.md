# Optional local speech features

The standard installer includes the small English speech engine. Accurate speech recognition, enhanced meeting recognition, semantic search and voice matching have separate optional local setup paths.

## Text modes

Choose Raw text or Modified text in Preferences > Writing > Text mode. Raw text preserves the recognizer output, including fillers and repetitions. Modified text removes fillers and repeated starts and applies basic formatting. Both modes work without a sentence-editing model.

## Optional setup

Use `scripts/setup_accuracy.ps1` for accurate local dictation or `scripts/setup_quality.ps1` for enhanced local notes and voice grouping. Optional models may need a first download. The basic installer does not need Python or a cloud account.

## Originals and review

Saved recordings retain original audio and recognized text. Meeting speaker names and voice grouping remain editable suggestions; real meeting and desktop acceptance require user testing. Confirmed vocabulary entries can help recognition, while Raw text bypasses text replacements.
