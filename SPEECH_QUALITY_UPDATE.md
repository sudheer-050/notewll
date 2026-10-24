# Current local accuracy candidate: 0.8.1

For accurate shortcut dictation run `scripts/setup_accuracy.ps1` in a normal PowerShell window. On this computer the dependencies and pinned model cache are already present. `Update Notewll accuracy.cmd` applies the verified local build using that environment. Wait for Ready before recording.

Accurate dictation uses local CPU Parakeet and Silero speech detection, with deterministic formatting. It does not wait for Ollama. The Accurate local dictation preference is separate from Enhanced local notes and meeting voices. Setup may download model files; runtime recognition stays local. No accuracy percentage is established yet. See verification/ACCURACY_0.8.1.md.

The following describes the earlier enhanced notes implementation; its shortcut/background grammar description is superseded by the section above.

# Enhanced local speech preview

This update adds Whisper small.en recognition for notes, audio-file checks and explicit recovery, local Ollama grammar editing, and Resemblyzer voice profiles for new meeting recordings. Shortcut dictation now uses continuous Vosk recognition and background grammar correction to reduce the delay on release. Existing recordings and their text are preserved. Original meeting audio is now saved without applying the quiet-noise gate to the stored track.

## Enable on this computer

Close any active recording first. In a normal PowerShell window run:

```powershell
cd C:\Users\gsudh\Documents\projects\local-dictation
.\scripts\setup_quality.ps1
```

The script builds the app, validates the speech models, backs up the installed executable and preferences, installs the worker, enables enhanced speech, and opens Notewll. Python, Whisper, Resemblyzer, Ollama and the llama3.2 model are needed for the enhanced mode. The existing Python environment, speech models and llama3.2 model on this computer were checked; fresh computers may need model downloads. The standard installer still supports the original small-model mode without Python.

The grammar worker starts a local Ollama service on 127.0.0.1:11439 when needed. It does not upload speech or text. Shortcut recognition updates every 250 ms, even when live insertion is disabled or held modifiers delay typing. Grammar warms at startup and works on partial sentences in the background. Corrections are reused only for matching text or an unchanged whole-word prefix. Release finalizes the streaming recognizer and gives grammar at most another 600 ms; if a correction is unavailable, ordinary local formatting/filler cleanup finishes the dictation. A late correction never rewrites completed dictation. This wait budget does not guarantee a subsecond microphone-to-application response on every PC. Skipping the second Whisper pass trades recognition accuracy for speed. Code style and disabling cleanup bypass grammar editing. Settings > Writing includes an enhanced-mode toggle. Notes, file checks and recovery retain enhanced recognition.

## Review meeting speakers

New enhanced recordings group voice samples into editable speaker suggestions. Explicit introductions can attach a name to the matching voice's earlier and later turns. No introduction means a numbered speaker. Short or uncertain audio can remain “Unassigned speaker.”

The beginning of the conversation contains a speaker index with the first few recognized lines. Select **Speakers and voice samples**, select a statement and choose **Play selected voice sample**. Playback stops after that statement or eight seconds. **Select same voice** selects the matching group so a name can be assigned to it. Save labels to persist changes. Original audio stays in its source track; the sample references its time range instead of making extra audio copies.

Voice grouping uses an online cosine threshold, not a calibrated identity guarantee. Short turns, overlapping voices, similar voices, room changes and mistaken introduction recognition need manual review. Profiles are scoped to each recording and capture source; this is not a global voice identity database. Saved meeting words remain verbatim recognition; grammar editing applies to dictated writing. No promise of perfect grammar or accurate human-meeting diarization is made.

## Verification and installation status

The application and installer compile. Regression, deterministic voice-grouping/name-linking/persistence checks, numeric-fact protection, local grammar examples, silent-audio checks, and the speaker-review UI smoke check were run. The UI screenshot was visually inspected. The grammar examples preserve meaningful “like,” negation and numeric amounts and remove contextual fillers.

Windows denied creating the executable backup in the installed application folder during the managed session. The installed app was reopened unchanged. Run the command above in your normal Windows session to install and enable the preview. The original preference file and saved notes were not changed by that failed installation attempt.

Real human speech accuracy, multi-person diarization and real-app final text replacement still require desktop acceptance. Windows speech synthesis and external fixture downloads were unavailable in this session. A non-speech tone probe stalled and was terminated; a speech-presence gate was subsequently added before Whisper decoding. Silence was checked separately. The similarity tests use controlled vectors and do not establish recognition accuracy for actual voices.

For implementation references, see [Whisper](https://github.com/openai/whisper), [Resemblyzer](https://github.com/resemble-ai/Resemblyzer), and [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs).


## Personalization follow-up

Saved Word entries now guide Whisper decoding as bounded vocabulary hints, refreshed on each request. Snippet expansions are excluded. Known Word corrections also run before grammar editing, so a recurring recognition error can be corrected before the grammar model interprets it. Original recognized meeting words are preserved. The Playground now displays the final corrected result rather than discarding it, and the audio-file check uses the enhanced recognizer when enabled.

To teach a recurring spelling or phrase, select the wrong words in Playground, choose **Remember a correction**, enter the correct text, then **Save preferences**. Manage or remove these entries under **Preferences > Writing > Words & snippets**. The next enhanced recognition request uses the saved vocabulary. Nothing is learned from arbitrary keystrokes or unconfirmed transcripts.

This is correction-based vocabulary personalization, not acoustic fine-tuning or automatic learning of a voice from ordinary use. Actual voice training requires representative audio with corrected reference transcripts and held-out evaluation. No improvement percentage is claimed without that evaluation.

The follow-up build passed 73 selected regression checks (14 excluded) and the UI smoke check. Tests cover current-request vocabulary propagation, snippet exclusion, word boundaries, serialization, and bounded prompts. The earlier enhanced build was installed and enabled by the user; rerun `scripts/setup_quality.ps1` from normal PowerShell to install this follow-up. Saved preferences, words and notes are retained.
