import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_update_preflight_checks_named_detector_without_touching_installation(tmp_path, monkeypatch):
    empty_appdata = tmp_path / "empty-appdata"
    monkeypatch.setenv("LOCALAPPDATA", str(empty_appdata))
    result = subprocess.run(
        ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
         str(ROOT / 'scripts/install_quiet_dictation.ps1'), '-CheckOnly'],
        capture_output=True, text=True, timeout=30,
    )
    assert result.returncode == 0, result.stderr
    fields = result.stdout.strip().split('|')
    assert fields[0] == 'update-preflight-ok'
    values = dict(field.split('=', 1) for field in fields[1:])
    assert set(values['applications'].split(',')) == {'Notewll.exe', 'LocalDictation.exe'}
    assert values['speech-detector'] == 'verified'
    assert values['installed-files'] == 'unchanged'

    assert not empty_appdata.exists()
