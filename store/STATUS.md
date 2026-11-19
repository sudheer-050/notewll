# Notewll Microsoft Store preparation

Product: Notewll (myhai)
Store ID: 9MTSFVDZD1C2
Submission: 1152921505702228860
Partner Center: https://partner.microsoft.com/en-us/dashboard/products/9MTSFVDZD1C2/overview

## Update - November 14, 2026 (submitted)

- WACK run on 0.11.1.0: overall PASS (report: verification/wack-0.11.1.0/wack-report.xml). The Blocked-executables test flags powershell.exe/reg use in Notewll.exe plus false positives in model binaries.
- Partner Center draft: package replaced 0.10.5.0 -> **0.11.1.0** (Validated). runFullTrust explanation updated to mention user-requested powershell.exe/reg setup. All six sections Complete.
- **Submitted for certification on 2026-11-14.** Status: In certification (pre-processing). Publishing hold remains: "Don't publish until I select Publish now". Not yet published.
- Still unverified: installed-package desktop acceptance (microphone, Notepad typing, meeting capture, startup, update, uninstall).

## Update - November 14, 2026

- Project moved to C:\Users\gsudh\Projects\local-dictation. Default Holly STT launcher path in src/Core.cs updated to Projects\ollama-personal-model.
- Built Store package **Notewll-Store-0.11.1.0-x64.msix** (unsigned Store upload), SHA256 `E298F23170DCAC2482F3D0AA9080DA6979F56F65F6B345ABC4A5F1DBD5724ADD`. Staged offline speech check: speech-ready|offline=true.
- NOT done: WACK (needs Administrator; run scripts/run_wack.ps1), installed-package desktop acceptance, upload of 0.11.1.0 to Partner Center, submission for certification, publication.
- Partner Center draft still holds package 0.10.2.0.

## Current readiness review

See [SUBMISSION_REVIEW.md](SUBMISSION_REVIEW.md) for the live section-by-section check and next steps. All six submission sections, including age ratings, are now Complete. Publishing remains on manual hold. Games-only broadcast declaration, reviewer instructions, public privacy link and displayed version are fixed. Package 0.10.2.0 is saved and Validated; installed-package desktop acceptance and WACK remain pending. Older unfinished-age-rating notes below describe the initial preparation, not the current portal state.

## Current fixes - October 25, 2026

- Saved and reload-verified Store package **0.10.2.0** as Validated, with all six sections Complete and manual publishing hold selected. Store app remains In draft.
- Corrected the Games-only declaration and reviewer workflow.
- Published https://myhai.org/projects/notewll/privacy.html from the existing policy and corrected the Notewll footer through the shared catalog generator. Final catalog revision: `3cc2036215c06955`.
- App version labels now follow compiled metadata; Store compiler metadata follows the MSIX version.
- Store artifact test and staged theme, recording shortcut, UX and offline speech checks passed; file version equals `0.10.2.0`.
- SHA256: `8CD487B7727A68F79FCFC9A16C374F820CB435592273ACCC4BB5A33E83206286`.
- Proof: `verification/store-submission-fixes/partner-center-overview.png`, `privacy-page.png`.
- This PC still has Store-format **0.10.0.0** installed. No package upgrade, certificate trust change, certification submission or Store publication was performed.
- Remaining: signed-package desktop acceptance (real microphone, typing, meeting playback, note-preserving update, startup, offline launch and uninstall), WACK, Microsoft certification including runFullTrust, then manual publication.

## Earlier draft update - October 25, 2026


- Replaced the earlier uploaded package with **Notewll-Store-0.10.1.0-x64.msix**, containing the latest UI/UX changes and current source.
- Partner Center verified the package as **Validated**; Packages is **Complete**. Only 0.10.1.0 remains in the saved submission.
- Updated English release notes and the text-review feature description; replaced all four desktop screenshots and added captions. Reload verified the saved text, captions and four 1914 x 1359 images.
- Product remains **In draft**, with Submit for certification available. Certification and public publication were not initiated.
- Local Store package test passed. Exact Store-compiled theme, shortcut and UX smoke checks passed. Bundled offline speech check: `speech-ready|offline=true`.
- Package SHA256: `99ECF09C0FC30DE0242A101A02ED5D4D6FF6EA4FBCCDA6E678516DE9125A7452`.
- Proof: `verification/store-update-0.10.1/partner-center-overview.png`.
- The known installed-package desktop acceptance/WACK limitations below remain unverified; package validation is not certification.

Earlier preparation notes below describe the initial 0.10.0.0 draft and are retained as history.

## Completed on October 25, 2026

- Reserved Notewll; saved Microsoft-assigned identity in store/identity.json.
- Built and uploaded dist/Notewll-Store-0.10.0.0-x64.msix (unsigned Store upload; Microsoft signs approved Store packages).
- Partner Center accepted package validation; Packages status Complete. runFullTrust remains subject to certification approval.
- Free, public, worldwide availability saved. Properties saved as Productivity, personal information accessed, microphone required, publisher support gsudheer56@gmail.com.
- Privacy policy supplied directly through Partner Center's supported privacy-text field and bundled as PRIVACY.md. No separate public privacy website was deployed.
- English listing saved with description, eight features, release notes, short description, and four actual app renders using isolated demonstration notes.
- Restricted capability explanation and Additional Testing Info reviewer instructions saved. Publishing is held until Publish now is selected.
- Age ratings preview generated: ESRB Everyone / PEGI 3 and regional equivalents. IARC legal agreement remains UNCHECKED and ratings not finalized.

## Verification

- makeappx pack: passed schema/payload validation.
- Staged offline speech check: speech-ready|offline=true.
- Artifact test: passed identity, hash, payload, privacy text, no private certificates/recordings/config, distribution channel and speech load.
- Focused pytest: 71 passed, 2 skipped, 2 deselected (live microphone tests).
- Including live microphone tests: 71 passed, 2 skipped, 2 failed; managed session could not capture microphone audio.
- Live insertion smoke: blocked in managed session (Live insertion was blocked).
- Add-AppxPackage -Register: denied, HRESULT 0x80070005. Installed package startup, recording, insertion and upgrade/uninstall remain unverified.
- Windows App Certification Kit could not launch here (0xc0000142); no WACK pass is claimed.
- Direct-download app also builds successfully. Existing installer was not rebuilt in this task.

## Remaining before public release

Theme selection now displays eight clickable miniature app previews in a four-by-two grid instead of a text-only dropdown. Cards preview each palette without changing the active theme while drawing; selecting a card updates the full app and its checkmark without saving until requested. All eight click paths, selection state, persistence and palette contrast pass the theme regression check; focused theme/shortcut/updater checks passed (7 tests). This visual picker is local-only until installed and is not in the uploaded Store package.

The latest local build adds eight selectable application themes in Preferences > App: System, Light, Dark, Bookshop, Ocean, Forest, Rose and Midnight. Theme previews do not write settings; saving persists the choice and reopening restores it. System remains the default; high-contrast colors override custom palettes. Generated Home/Notes/settings/voice-setup renders and a local gallery are under output/theme-gallery/index.html. Regression checks passed (17 tests), followed by 7 focused checks after final preview/reopen refinements. The Store upload still contains the earlier build; the normal-desktop updater is required to install these changes locally.

Voice enrollment in the latest local build now supplies a short story, "A bookshop from tomorrow," split into three six-second reading passages. The current passage remains visible during recording. Six no-microphone UI previews rendered successfully; voice-matching/updater checks passed (7 tests). The revised enrollment screen is not in the uploaded Store package and requires the normal-desktop updater to install locally.

The latest local build adds user-configurable Dictate, Note and Meeting shortcuts together in Preferences > Recording. Dictate holds to record; Note and Meeting toggle their own recording. Hands-free has no action shortcut. Existing Dictate/Note bindings are preserved when migrating settings; a nonconflicting Meeting default is chosen. This change is not in the uploaded Store package and must be installed with the normal-desktop updater for physical shortcut acceptance.

The local app build also includes a fourth floating-ball action for hands-free listening, wired to the existing opt-in voice-match/enrollment safeguards. Preparing/active states and pause-on-click are verified in the toolbar smoke; four controls fit without overlap at tested DPI scales. The preview picker matches the four-action menu. Focused toolbar/ball/updater checks passed (4 tests). This UI change is not in the uploaded Store package and is not confirmed installed until the normal-desktop updater is run.

Typing defect reported after initial submission preparation: recognition/history is correct but Notepad receives punctuation instead of words, including with live writing disabled. A candidate transport fix now uses verified target-layout keyboard events with paced character pairs and preserves Unicode for unmappable characters. The direct app builds; keymap regression passes, including Caps Lock and Unicode. Desktop injection tests remain blocked here. The installed app has NOT been replaced (installed-folder write probe denied). Run Update Notewll typing.cmd on the normal desktop, then test the exact reported sentence. The Store package already uploaded does NOT contain this candidate fix; rebuild and replace that package only after desktop acceptance.

1. User accepts IARC Terms of Use / age-of-majority declaration and saves the rating.
2. Run clean Windows installed-package acceptance: offline launch, physical microphone/silence, F8 and cross-app typing, microphone/playback notes, export, startup, upgrade/uninstall and WACK.
3. Submit for Microsoft certification; address any capability or certification findings. Manual publishing hold prevents automatic public release.

## Rebuild

powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build_store.ps1
python -m pytest tests/test_store.py -q

Rebuilds use a fresh staging directory under output/store-build-* and verified cached Vosk archives. Build metadata and SHA256 are in dist/Notewll-Store-build.json. Rebuilding changes the local package; upload the new artifact if you intend to replace the saved draft.
