# Notewll 0.11.0 Windows preview verification

Reviewed October 29, 2026. This is an unsigned preview, with no Microsoft Store submission.

- Application and installer report 0.11.0; installer extraction verifies the exact application payload and bundled offline English engine.
- Local applicable test suite: 177 passed, 3 skipped, 3 deselected. The exclusions are two physical microphone tests and a legacy console test belonging to the separate Holly repository.
- UI audit: 210 captures across Light/Dark, compact/normal pages, open Create and settings dropdowns, and dialogs; zero layout findings. Screenshots use isolated demonstration data.
- Focused checks cover row selection, bulk selection, search, immediate entry saving, rejected-save rollback, settings cancellation, atomic notes, command editing, calendar parsing, optional speech protocol and installer resources.
- The settings smoke check uses isolated defaults and notes, so saved language/style preferences cannot change its expected output.
- Update preflight checks validate the staged payload without requiring or changing an installed copy. The install guard remains in place for actual updates.
- Store-specific packaging tests require a separately built local MSIX; a clean source checkout skips that artifact test. No MSIX is published by this GitHub release.

Installer SHA256: `895a47678d25320d9048b90a1fde4d1ff0a41c603867581a04a40e45347eae09`.

## Acceptance boundaries

These checks do not establish physical microphone/call capture, live typing in external apps, multilingual recognition accuracy, mixed-DPI hardware or Narrator acceptance. No human-reference WER or new universal latency guarantee is claimed. General generative rewriting, cloud calendar accounts, recurring ICS events, team sharing and mobile are outside this Windows release. Existing experimental macOS source is preserved separately.

Standard English speech stays local and is bundled. Optional models/runtimes download only during explicit setup. Formatted snippet paste is opt-in and clipboard restoration is best effort. Voice edits are explicit commands with a review step. Notes and original recordings are preserved through updates.

The updater requires a trusted publisher signature; this unsigned installer is a manual download.
