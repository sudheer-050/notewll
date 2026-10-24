# Notewll 0.8.0 release verification

- Application and full Windows installer compiled with application and setup versions 0.8.0.0.
- 91 automated application, setup, native-engine, export, speech-worker, UI and regression checks passed. Four live microphone/keyboard checks were excluded.
- Command: `python -m pytest -q tests --ignore=tests/test_console_visibility.py --basetemp output/release-080-verified -k "not test_live_default_microphone and not test_live_missing_named and not test_live_holly and not test_live_actual_keyboard"`.
- `test_console_visibility.py` exercises a separate Holly repository and is excluded from this project's release source snapshot and count.
- Two preview checks initially failed because an empty optional result overwrote computed Playground cleanup. The shared transcript handler was fixed and both checks pass in the 91-test run.
- Installer embedding/hash inspection and direct silence/cancellation handlers passed.
- Streaming replay timings and their limits are documented in STREAMING_DICTATION.md: 509, 416 and 148 ms after simulated release on three six-second local recordings. These are not physical shortcut-to-typing measurements or a universal speed/accuracy guarantee.
- Installer is unsigned; automated publisher-certificate verification remains required by the updater. No signing certificate, fake signature or automatic-update bypass was added.
- Real microphone capture fails with MMSYSERR 1 in the managed environment and keyboard injection is unavailable. Normal Windows desktop acceptance remains necessary.
- Publication excludes recordings, local preferences, Python/model environments, output directories and unrelated application source. The native runtime/model remain pinned first-install downloads; enhanced Python/Ollama setup remains optional.
