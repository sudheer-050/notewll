import json
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'build/Notewll.exe'


def run(*args):
    return subprocess.run([str(EXE), *map(str, args)], capture_output=True, text=True, timeout=30)


def test_three_shortcut_recorders_save_cancel_and_preserve_other_actions(tmp_path):
    result = run('--recording-shortcuts-smoke', tmp_path)
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'recording-shortcuts-ok|three-actions|preserved-existing|capture|cancel|save-reload|duplicates-rejected'


@pytest.mark.parametrize('dictate,note', [('Next+Down', 'Ctrl+Shift+N'), ('Ctrl+Shift+M', 'F9')])
def test_legacy_config_gets_nonconflicting_meeting_shortcut(tmp_path, dictate, note):
    config = tmp_path / 'old.json'
    config.write_text(json.dumps({'Hotkey': dictate, 'NotesHotkey': note}))
    result = run('--recording-shortcuts-config', config)
    assert result.returncode == 0, result.stderr
    values = result.stdout.split('|')
    assert values[:2] == [dictate, note]
    assert len(set(values)) == 3


@pytest.mark.parametrize('meeting', ['F8', 'Ctrl+Shift+N'])
def test_meeting_shortcut_cannot_duplicate_either_other_action(tmp_path, meeting):
    config = tmp_path / 'duplicates.json'
    config.write_text(json.dumps({'MeetingHotkey': meeting}))
    result = run('--recording-shortcuts-config', config)
    assert result.returncode != 0
    assert 'different shortcut' in result.stderr
