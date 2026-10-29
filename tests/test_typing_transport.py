import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'build/Notewll.exe'


def test_keyboard_events_preserve_sentence_punctuation_unicode_and_caps_lock():
    result = subprocess.run([str(EXE), '--typing-keymap-smoke'], capture_output=True, text=True, timeout=20)
    assert result.returncode == 0, result.stderr
    assert result.stdout.startswith('keymap-ok|caps-on-and-off|unicode-preserved|ordinary-characters=')


def test_real_typing_preserves_words_in_plain_and_rich_editors():
    result = subprocess.run([str(EXE), '--typing-editor-smoke'], capture_output=True, text=True, timeout=40)
    if result.returncode != 0 and 'Desktop keyboard injection is blocked' in result.stderr:
        pytest.skip('Desktop input is unavailable; run the typing check on the normal Windows desktop.')
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'typing-ok|plain-and-rich-editors|exact-sentence|live-revisions|surrounding-preserved'
