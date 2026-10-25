# Notewll 0.9.0 user guide

## Install and start

Run `dist/Notewll-Setup.exe` and click Install. This single file includes the application and standard English speech model/runtime; installation and standard speech work offline. No Python or command-line setup is needed. Exit the old app if requested. Setup reports successful installation separately and offers Open app. Start opens the hub; closing it leaves the app running. Exit in the Windows system tray quits it.

Home offers Dictate, New note, Record meeting and recent work. First-run guidance includes permission and practice steps. Preferences > Recording has microphone testing, Check speech engine and setup repair.

## Updates

Download the latest installer from [GitHub Releases](https://github.com/sudheer-050/notewll/releases) and run it again; setup preserves existing notes, recordings and preferences. About > Check for updates opens that page when a signed update feed is not configured. Automatic updates require a trusted publisher certificate and update feed.

## Write into other applications

Click your target field, hold F8 and speak, then release to refine. Escape cancels. In Recording, use the shortcut recorder to press/release another combination. Function keys support the most direct live writing; held Ctrl/Alt/Shift/Win combinations can delay insertion until release. Windows or other apps may intercept shortcuts.

Avoid moving the cursor or typing during capture. If insertion is interrupted, Recover last dictation shows the final text and warns about partial insertion. Retry processes retained audio without automatically pasting it; Copy explicitly changes the clipboard; Discard clears recovery. Recovery lasts until exit or the next attempt. An ordinary successful paste attempts to restore your previous clipboard unless you copied something newer.

Dictation types directly into the focused text field without inspecting its contents or automatically retrying by paste. Click inside the message field before starting; release shortcut modifiers before text is inserted. With live writing disabled, the finished transcript is typed on release.

Accurate local dictation is an optional preference under Writing. Run `scripts/setup_accuracy.ps1` once if its local environment/model is missing, and wait for Ready before recording. This mode uses local Parakeet speech recognition and Silero speech detection. Practice, file tests and recovery use the same chosen dictation engine. Cleanup handles punctuation/casing and fillers; it does not infer missing words or rewrite facts. Switch off accurate dictation for the basic small-model mode. Existing enhanced-speech preferences migrate to accurate dictation, while the enhanced meeting preference remains independent.

The local model warms before accurate recording is enabled. Readiness is shown in the app and toolbar status, and cancel does not block behind an in-flight inference. Silence and rejected noise end without a warning or clipboard change. Speech detection can still miss quiet/short speech or accept some real-world noise; the finite synthetic controls do not establish perfect detection. One longer test replay took about two seconds after release, so a one-second response is not guaranteed.

Writing preferences select cleanup/verbatim, live writing, terminal paste and local dictionary/snippets. Windows Terminal commonly uses Ctrl+Shift+V; classic consoles use Shift+Insert. No Enter is added. Windows can block insertion into an administrator window. Home > Try dictation opens the optional inline microphone/recognition test. It previews cleanup and can transcribe a PCM 16 kHz mono WAV. Practice is collapsed by default and does not appear as a separate navigation section or add entries to history.

## Dictionary and snippets

Dictionary > Add word now records your pronunciation instead of asking you to type a recognition mistake. Press Record pronunciation, say the word or name, then Stop (five seconds maximum). Review the read-only Heard result, enter Correct spelling, and choose Use word. Listen replays the sample; Record again replaces the draft. Save preferences activates the correction and stores the WAV locally in the Pronunciations folder beside config.json. Cancelling leaves the active entry unchanged and does not write draft audio.

The current local recognizer transcribes the recording. Dictionary pairs that recognized text with your correct spelling; saved Word spellings also supply hints to the optional enhanced recognizer. This does not retrain the speech model or perform acoustic voice matching. Existing text-based word entries keep working; editing one asks for a pronunciation recording. JSON rule exports contain correction rules, not the separate pronunciation WAV files.

Snippets still use a typed trigger and saved expansion, such as saying "my signature" to insert a full sign-off. Snippets do not become speech-recognition hints.

## Floating ball controls

Click the ball to reveal Dictation, Personal notes and Meeting icons in a half-circle menu. Each black icon sits on a white round tile at 50% opacity. Dictation writes into the focused field; activate again to stop. During transcription, click the processing indicator or press Escape to cancel. Notes and Meeting controls start and stop their respective recordings.

Drag the ball and release to throw it. Gravity, collisions and rolling friction bring it to rest at the bottom edge. The collapsed ball and expanded menu have separate size controls in Preferences > Ball. Click opens the menu; moving away collapses it. The Windows system tray also offers the full app, one-hour hide, Show toolbar and a keyboard action menu.

## Notes and meetings

Start without a title/name. Content and recognized introductions fill them; Edit details corrects them later. Manual title/name edits are preserved. Standard recordings label their audio source. Optional enhanced meeting recognition adds editable voice groups; review suggested speakers rather than treating them as verified identities.

Next recording selects My microphone, Computer audio or Both sources. Devices chooses endpoints; use the output selected in Zoom/Meet. Recorded-source metadata is separate from the next-recording dropdown. Watch both meters when capturing both. Headphones reduce duplicates. Pause preserves elapsed time in the same note; Resume continues. Stop saves audio and finishes recognition. Device errors preserve audio: select a working device and use More > Resume where available.

Preferences > Notes uses one dropdown-selected toggle shortcut, with separate personal/meeting source defaults. Press once to start and again to stop without opening the hub. The toolbar makes shortcuts optional.

Search saved text, names, titles and tags, filter notes/meetings, resize the library, and sort by Newest, Oldest, Title or Longest. You can review another record while recording. My notes holds editable annotations; Conversation preserves recognizer words read-only. Ctrl+F/F3 finds text. Double-click timestamps for source playback with seek/speed. Missing ranges and Retry appear outside spoken text. Silence and engine errors are not inserted into conversations. Quiet-noise suppression cannot reject all noise.

Local review creates a source-quote overview and possible decisions/follow-ups. Type keywords or a question to retrieve matching original statements with timestamps. It does not infer facts or generate AI answers. More > Correct speaker labels edits attribution metadata without changing words. + Written note creates a note without audio.

## Export, storage and recovery

Export previews Conversation only, Personal notes only or Full record. Participant metadata is optional; names spoken inside the conversation remain. Export as a view-oriented PDF; editable Word, Excel and CSV exports are no longer offered. Saved files can be opened immediately. PDF supports text search/copy and contains reading-order structure tags. External PDF copies can still be altered with specialist tools. These are not tamper-proof originals.

More > Delete moves the record/audio to Recently deleted. Preferences > Storage shows app/model, recordings, deleted items and cache. Restore brings records back; permanent deletion needs confirmation. Retention is opt-in, with Forever as default. Backup creates a ZIP of active notes/audio; Restore backup skips existing IDs. Deleted records, preferences and the speech model are excluded.

Uninstall through Windows Installed apps. Data is retained by default. Downloaded-component removal and permanent local-data removal are separate choices; erasing data needs another confirmation. Notes live in `%LOCALAPPDATA%\LocalDictation\Notes`.

## Troubleshooting

- Missing engine: Check speech engine, then rerun full Setup; the full installer includes the standard speech files.
- Missing microphone: select the input, check its meter and Windows microphone permission for desktop apps.
- Missing call output: choose the actual device used by the call; capture includes all apps using it.
- Soft speech missing/noise transcribed: try disabling quiet-noise suppression and improve microphone placement. The small English model may misrecognize names.
- Failed dictation: Recover last dictation before exit to retry retained in-memory audio.
- Missing note words: inspect coverage status and Retry saved WAV audio.
- Updates unavailable: no signed public update feed is configured. About shows the version; use a newer trusted full installer when available.

This release is Windows x64. Real microphone, meeting, playback and cross-app operation still need desktop validation. BACKLOG_STATUS.md tracks remaining audit work.


Version 0.7.0 adds explicit Standard, Chat, Email and Code writing styles, a searchable Words & snippets editor, and user-controlled Record/Stop on Practice. Closing recording-device selection without Apply cancels changes. Setup records successful microphone and recognition checks, then asks you to confirm typing into another app. Conversations remain read-only; titles, participant labels and personal notes can be edited separately.


## Floating ball and customization

Click the ball to open its half-circle action menu. The three icons control dictation, personal notes and meeting notes. Drag and release the ball to throw it; it falls, bounces and settles at the bottom edge. Reduce motion in Preferences > App to skip the movement.

In Preferences > Ball:

- Choose a sports-ball or planet appearance. Color and Gradient are plain appearances; their color controls appear only when selected.
- Pick colors from the continuous color range or enter a hex color. The picker previews both the ball and expanded tray before you confirm.
- Choose a gravity environment to play a sample drop and bounce automatically. Use Drop preview to replay it. Appearance and gravity are independent; planet skins use cricket-ball properties.
- Ball size shows percentages, with 100% preserving the selected appearance's original size. Its limits approximate 0.5-2.5 cm using display scaling; exact physical measurements depend on the screen. Expanded ball size independently ranges from 100% to 200%, including its icon tiles.
- Choose local image opens a file browser for PNG, JPEG, BMP or GIF images. The app keeps a normalized local copy, centers it inside the ball, and rotates it with the surface. Remove image removes the overlay without deleting the original file. GIFs use a still frame.
- Apply saves the choices; Reset ball defaults restores the default moon, Earth gravity, default sizes and no custom image.

To verify this feature from a source checkout, run `scripts/build.ps1`, then `build/LocalDictation.exe --ball-preferences-smoke output/ball-preferences`. Additional focused checks are under `verification/verify_ball_physics.ps1` and `verification/verify_tray_*.ps1`.


## Optional hands-free, my voice only (0.10.0 preview)

1. Open **Preferences > Hands-free** and choose **Set up local voice matching**. This reuses compatible enhanced-speech dependencies if present, or offers optional package setup (Python 3.10+ is required).
2. Save your preferred microphone, then choose **Enroll / replace my voice**. Record three six-second samples in a quiet room. No enrollment audio is saved; the local voice profile is encrypted for your Windows account.
3. Choose **Start hands-free**, click the text field you want to write into, and speak a full phrase. Pause for verification and transcription. Merely hovering does not select a typing destination. Very short phrases and uncertain voices are discarded.
4. Click the listening ball or press **Escape** to stop the mic immediately. Normal shortcuts and note recording pause this mode. It never enables itself at launch.
5. **Delete my voice profile** pauses listening and removes the saved profile. Re-enroll when changing microphones if matching becomes unreliable.

Keep control of your destination: physical input or focus/caret changes discard pending phrases. Busy inference drops incoming speech instead of building an ambient recording. There is no automatic clipboard/paste fallback. Voice matching is not perfect, cannot reliably reject overlapping speech or playback of your voice, and does not distinguish dictation from your other conversations. Pause when you do not want your words typed. Native password Edit controls are blocked, but browser password fields cannot reliably be detected. See the verification report for the desktop tests that remain.

### Find conversations and act on them

Open a saved entry and choose **Find & act**. Install semantic search through
**Preferences > Storage > Storage & recently deleted > Optional features** first.
Search by meaning returns source passages across saved entries; double-click one
to open its note and recording timestamp. Highlights select original excerpts.
Action items stay suggestions until you edit them, tick Confirm and save review.

Use **Edit details** to set a project and tags. History search accepts
`project:"Apollo release" tag:budget person:Alex` alongside words.
During recording, Ctrl+Alt+1 marks Important, Ctrl+Alt+2 marks Question and
Ctrl+Alt+3 marks Follow up, even while another app is focused. Review bookmarks
after the recording finishes.

Hands-free requires an allowed-app list in Preferences, for example
`notepad, chrome`. An empty list blocks all apps. Locking Windows or changing
microphones pauses listening. Start it again manually when ready.

In PDF export, choose the scope, select sensitive preview text and click
**Redact selected text**. Only that PDF text is redacted; source audio is unchanged
and is not included. Changing the scope resets redactions.

Storage > Backups offers encrypted snapshots and an opt-in daily schedule while
Notewll runs. Encrypted snapshots require this Windows account on this PC. Use ZIP
for migration. Restore previews titles, sizes and skipped existing notes before
import. Scheduled snapshots skip active recordings and remain until removed.

### Raw text and Modified text

Choose **Preferences > Writing > Text mode** and save your preferences.

- **Raw text** keeps the recognizer output unchanged, including fillers and repeats. It bypasses dictionary replacements and writing-style formatting.
- **Modified text** removes fillers and repeated starts and applies basic formatting and your selected writing preferences.

Compare RAW TEXT and MODIFIED TEXT in the Home microphone test. The recognizer may still mishear words or supply punctuation. Both modes run locally without a sentence-editing model.
