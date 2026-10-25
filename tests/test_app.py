import json
import os
import subprocess
import tempfile
from pathlib import Path

import pytest

ROOT = Path(__file__).parents[1]
EXE = ROOT / "build" / "Notewll.exe"


def run(*args, timeout=120):
    # Notewll.exe is a GUI-subsystem binary; piping its output makes the shell wait for it.
    return subprocess.run([str(EXE), *args], text=True, capture_output=True, timeout=timeout)


def clean(text):
    return run("--cleanup", text).stdout


def holly_up():
    return run("--stt-health").returncode == 0


def has_mic():
    return bool(run("--list-devices").stdout.strip())


# --- installer / privacy ----------------------------------------------------

def test_installer_stops_running_app_before_replacing_binary():
    source = (ROOT / "installer" / "Setup.cs").read_text(encoding="utf-8")
    assert source.index("if (!StopRunning())") < source.index("File.WriteAllBytes(exe")
    assert "LocalDictation.Exit" in source and "WaitForExit(8000)" in source


def test_dictation_does_not_save_audio_or_transcripts_to_disk():
    source = "\n".join(p.read_text(encoding="utf-8") for p in (ROOT / "src").glob("*.cs") if p.name in {"App.cs", "Audio.cs", "Core.cs", "Stt.cs", "Recovery.cs", "LiveWriting.cs", "Shortcut.cs", "Overlay.cs", "FloatingDock.cs"})
    for forbidden in ("File.WriteAllBytes", "File.WriteAllText(", "FileStream", "StreamWriter", "System.Speech"):
        if forbidden == "File.WriteAllText(":
            # the only permitted write is the config file
            assert source.count(forbidden) == 1 and "Serialize(this)" in source
        else:
            assert forbidden not in source, forbidden


# --- cleanup: removes noise ---------------------------------------------------

def test_cleanup_removes_fillers_and_stutters():
    assert clean("um hello the the world, uh this is local") == "Hello the world, this is local."


def test_cleanup_scratch_that_drops_previous_sentence():
    assert clean("Send it Tuesday. Scratch that. Send it Wednesday.") == "Send it Wednesday."


def test_cleanup_scratch_that_keeps_earlier_sentences():
    assert clean("The report is final. Book Tuesday scratch that book Wednesday") == "The report is final. Book Wednesday."


def test_cleanup_spoken_swap_with_comma():
    assert clean("Meet on Tuesday, I mean Wednesday at 3.") == "Meet on Wednesday at 3."


# --- cleanup: preserves meaning ------------------------------------------------

@pytest.mark.parametrize("text", [
    "I did not send it.",
    "He said that that was fine.",
    "She had had enough.",
    "What I mean is we should wait.",
    "I mean it.",
    "The period of time was long.",
    "Call 555 0100 before 3.30.",
    "Please don't delete the file.",
])
def test_cleanup_leaves_meaningful_text_unchanged(text):
    assert clean(text) == text


def test_cleanup_does_not_turn_words_into_punctuation():
    assert clean("add a comma and a question mark to the sentence") == "Add a comma and a question mark to the sentence."


def test_cleanup_empty_input_stays_empty():
    assert clean("um uh") == ""


# --- microphone selection ----------------------------------------------------

def test_device_default_resolves_to_system_default():
    assert run("--resolve-device", "system-default", "Mic A", "Mic B").stdout == "-1|False"


def test_device_saved_choice_is_found_by_name():
    assert run("--resolve-device", "Mic B", "Mic A", "Mic B").stdout == "1|False"


def test_device_missing_saved_choice_falls_back_to_default():
    assert run("--resolve-device", "Unplugged Headset", "Mic A").stdout == "-1|True"


def test_device_fallback_when_no_devices_listed():
    assert run("--resolve-device", "Mic A").stdout == "-1|True"


# --- config -------------------------------------------------------------------

def write_config(tmp_path, **values):
    path = tmp_path / "config.json"
    path.write_text(json.dumps(values))
    return str(path)


def test_config_defaults_are_private_and_opt_in(tmp_path):
    result = run("--validate-config", write_config(tmp_path, Hotkey="F10"))
    assert result.stdout == "F10|system-default|True|True|False"  # startup defaults to off


def test_config_loads_v010_file(tmp_path):
    result = run("--validate-config", write_config(tmp_path, Hotkey="F8", Microphone="system-default", RecognizerId="x"))
    assert result.returncode == 0


def test_config_accepts_named_microphone(tmp_path):
    result = run("--validate-config", write_config(tmp_path, Microphone="Microphone (Realtek(R) Audio)", CleanupEnabled=False, AutoPaste=False))
    assert result.stdout.endswith("|False|False|False")


def test_config_rejects_invalid_hotkey(tmp_path):
    result = run("--validate-config", write_config(tmp_path, Hotkey="Ctrl++Space"))
    assert result.returncode == 2 and "Choose a key" in result.stderr


@pytest.mark.parametrize("url", ["http://example.com", "https://127.0.0.1:5000", "http://192.168.1.5:5000", "ftp://localhost"])
def test_config_rejects_non_loopback_stt(tmp_path, url):
    result = run("--validate-config", write_config(tmp_path, SttUrl=url))
    assert result.returncode == 2 and "loopback" in result.stderr


def test_stt_client_refuses_remote_address_without_connecting():
    result = run("--stt-health", "http://example.com")
    assert result.returncode == 4 and "loopback" in result.stdout


def test_stt_down_is_reported_not_raised():
    result = run("--stt-health", "http://127.0.0.1:1")
    assert result.returncode == 4 and result.stdout.startswith("down|")


def test_settings_window_builds_with_devices_cleanup_preview_and_startup_off():
    result = run("--settings-smoke")
    assert result.returncode == 0, result.stderr
    assert result.stdout.startswith("settings-ok|mics=Windows default microphone")
    assert "|startup=False|" in result.stdout and result.stdout.endswith("preview=Hello hello there.")


def test_desktop_hub_navigation_preferences_preview_and_close_to_tray():
    result = run("--hub-smoke")
    assert result.returncode == 0, result.stderr
    assert result.stdout == "hub-ok|pages=7|preferences=applied|preview=working|close=tray"


# --- state --------------------------------------------------------------------

def test_state_happy_path():
    assert run("--state-sequence", "Recording", "Processing", "Idle").stdout == "Idle"


def test_state_error_recovers_to_idle():
    assert run("--state-sequence", "Recording", "Error", "Idle").stdout == "Idle"


def test_state_rejects_processing_without_recording():
    result = run("--state-sequence", "Processing")
    assert result.returncode == 3 and "Invalid state transition" in result.stderr


# --- live hardware / Holly STT (skipped when unavailable) ----------------------

@pytest.mark.skipif(not EXE.exists() or not has_mic(), reason="no capture device")
def test_live_default_microphone_captures_audio():
    result = run("--capture-test", "system-default", "1500")
    fields = dict(part.split("=", 1) for part in result.stdout.split("|"))
    assert result.returncode == 0 and int(fields["bytes"]) > 20000 and fields["fellBack"] == "False"


@pytest.mark.skipif(not EXE.exists() or not has_mic(), reason="no capture device")
def test_live_missing_named_microphone_falls_back_and_still_captures():
    result = run("--capture-test", "Definitely Not A Real Microphone", "1000")
    assert result.returncode == 0 and "fellBack=True" in result.stdout


@pytest.mark.skipif(not EXE.exists() or not holly_up(), reason="Holly STT not running")
def test_live_holly_stt_returns_transcript_for_speech():
    wav = Path(tempfile.gettempdir()) / "ld-pytest-tts.wav"
    script = (
        "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
        f"$s.SetOutputToWaveFile('{wav}'); $s.Speak('Please schedule the review for Wednesday.'); $s.SetOutputToNull(); $s.Dispose()"
    )
    try:
        subprocess.run(["powershell", "-NoProfile", "-Command", script], check=True, timeout=60)
        result = run("--transcribe-wav", str(wav))
        assert result.returncode == 0 and "schedule the review" in result.stdout.lower()
    finally:
        wav.unlink(missing_ok=True)


@pytest.mark.parametrize("shortcut,canonical", [("CTRL+SPACE", "Ctrl+Space"), ("shift+ctrl+A", "Ctrl+Shift+A"), ("Alt", "Alt"), ("A+B", "A+B"), ("F24", "F24")])
def test_custom_shortcut_config(tmp_path, shortcut, canonical):
    result = run("--validate-config", write_config(tmp_path, Hotkey=shortcut))
    assert result.returncode == 0 and result.stdout.startswith(canonical + "|")


def test_shortcut_repeat_and_release_order():
    result = run("--shortcut-sequence", "Ctrl+Space", "+Ctrl", "+Space", "+Space", "-Ctrl", "-Space")
    assert result.stdout == "0:False|1:True|0:True|-1:False|0:True|"


def test_shortcut_ignores_extra_keys():
    result = run("--shortcut-sequence", "Ctrl+Space", "+Shift", "+Ctrl", "+Space", "-Space")
    assert result.stdout == "0:False|0:False|0:False|0:False|"


def test_shortcut_recorder_and_cancel():
    result = run("--shortcut-recorder-smoke")
    assert result.returncode == 0 and result.stdout == "recorder-ok|Ctrl+Shift+Space|cancel-preserved"


def test_notes_recover_audio_names_and_failed_transcription(tmp_path):
    result = run("--notes-smoke", str(tmp_path / "notes"))
    assert result.returncode == 0, result.stderr
    assert result.stdout == "notes-ok|recovery|timeline|names|persistence|retry"
    note_file = next((tmp_path / "notes").glob("*/note.json"))
    note = json.loads(note_file.read_text(encoding="utf-8"))
    assert note["UserName"] == "Sudheer" and note["WrittenNotes"] == "Follow up tomorrow."
    assert (note_file.parent / "microphone.wav").exists()
    assert (note_file.parent / "computer.wav").exists()


@pytest.mark.parametrize("previous,next_text,expected", [
    ("um hello", "Hello.", "8|Hello."),
    ("hello the the world", "Hello the world.", "19|Hello the world."),
    ("hello", "hello there", "0| there"),
    ("raw draft", "", "9|"),
    ("same", "same", "0|"),
])
def test_live_text_refinement(previous, next_text, expected):
    assert run("--live-edit", previous, next_text).stdout == expected


@pytest.mark.parametrize("method,window_class,expected", [
    ("Auto", "CASCADIA_HOSTING_WINDOW_CLASS", "CtrlShiftV"),
    ("Auto", "ConsoleWindowClass", "ShiftInsert"),
    ("Auto", "Notepad", "CtrlV"),
    ("ShiftInsert", "Chrome_WidgetWin_1", "ShiftInsert"),
])
def test_terminal_paste_policy(method, window_class, expected):
    assert run("--paste-policy", method, window_class).stdout == expected


def test_duplicate_recording_shortcuts_are_rejected(tmp_path):
    result = run("--validate-config", write_config(tmp_path, NotesHotkey="F8"))
    assert result.returncode == 2 and "different shortcut" in result.stderr


def test_small_engine_uses_native_api_without_external_service():
    source = (ROOT / "src" / "Stt.cs").read_text(encoding="utf-8")
    assert "vosk_recognizer_partial_result" in source and "model-small-en-us" in source
    for dependency in ("HttpWebRequest", "Process.Start", "python.exe", "holly_server", "Docker"):
        assert dependency not in source


def test_live_actual_keyboard_insertion():
    result = run("--live-writing-smoke")
    if result.returncode != 0 and "blocked" in result.stderr:
        pytest.skip("Desktop keyboard injection is unavailable in this session")
    assert result.returncode == 0 and result.stdout == "live-ok|raw-inserted|refined|surrounding-preserved"


def test_floating_toolbar_buttons_focus_and_edge_snap():
    result = run("--toolbar-smoke")
    assert result.returncode == 0, result.stderr
    assert result.stdout == "toolbar-ok|buttons=3|no-focus-steal|edge-snap"


def test_automatic_note_details_respect_manual_title():
    result = run("--note-metadata-smoke")
    assert result.returncode == 0, result.stderr
    assert result.stdout == "metadata-ok|automatic-titles|manual-edit-preserved|written-notes"


def test_note_preferences_and_toolbar_position_persist(tmp_path):
    config = write_config(tmp_path, NotesHotkey="F11", NotesSource="Computer", MeetingSource="Both", ToolbarX=-500, ToolbarY=280)
    result = run("--note-preferences", config)
    assert result.returncode == 0 and result.stdout == "F11|Computer|Both|-500|280"


def test_old_note_shortcut_migrates_to_single_dropdown_choice(tmp_path):
    config = write_config(tmp_path, NotesMicrophoneHotkey="Ctrl+Alt+N", NotesComputerHotkey="Ctrl+Alt+F10", NotesBothHotkey="Ctrl+Alt+F11")
    result = run("--note-preferences", config)
    assert result.returncode == 0 and result.stdout.startswith("Ctrl+Alt+N|Microphone|Both|")


def test_invalid_meeting_audio_is_rejected(tmp_path):
    result = run("--validate-config", write_config(tmp_path, MeetingSource="missing-device-mode"))
    assert result.returncode == 2 and "meeting audio" in result.stderr


def test_ball_preferences_previews_and_saved_customization(tmp_path):
    result = run("--ball-preferences-smoke", str(tmp_path))
    assert result.returncode == 0, result.stderr
    for feature in ("171-physics-combinations", "plain-color", "conditional-color-controls",
                    "live-color-picker", "automatic-gravity-bounce", "independent-expanded-size",
                    "scaled-icons", "local-image-copy", "image-save-reopen", "image-rotation"):
        assert feature in result.stdout
