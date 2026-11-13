# Notewll 0.11.1 verification

This patch enables Windows startup and the floating ball by default for fresh installations. Setup configures startup using the installed application, preserves explicit existing opt-outs, and launches with `--background`. A duplicate background launch does not open the dashboard.

- Targeted startup, configuration, installer integrity, update and speech loading checks: 33 passed.
- Broader Windows feature run: 180 passed, one skipped, four live tests excluded. One additional test targets a missing `_free_vram_mib` helper in the separate Holly repository; that external test is not applicable to this patch.
- The installer contains the exact built application and loads its embedded offline English speech engine and model.
- Recognition algorithms and models are unchanged by this patch. No new speech-accuracy claim is made.
- Real sign-in startup, an interactive fresh install on a second PC, live microphone capture and cross-application insertion remain manual acceptance checks. This unsigned Windows preview is not a Microsoft Store submission.
