import json
from test_app import ROOT, run


def test_fresh_install_enables_windows_startup_and_floating_ball(tmp_path):
    path = tmp_path / 'config.json'
    result = run('--configure-startup', '--dry-run', str(path))
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'startup=True|ball=True|launch=--background'
    assert not path.exists(), 'Inspecting startup defaults must not change preferences'


def test_upgrade_preserves_explicit_startup_and_ball_opt_out(tmp_path):
    path = tmp_path / 'config.json'
    path.write_text(json.dumps({'StartWithWindows': False, 'ShowToolbar': False}))
    before = path.read_bytes()
    result = run('--configure-startup', '--dry-run', str(path))
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'startup=False|ball=False|launch=--background'
    assert path.read_bytes() == before


def test_installer_starts_background_only_after_startup_configuration():
    source = (ROOT / 'installer/Setup.cs').read_text(encoding='utf-8')
    assert source.index('Arguments = "--configure-startup"') < source.index('Arguments = "--background"') < source.index('                ShowSuccess();')
    program = (ROOT / 'src/Program.cs').read_text(encoding='utf-8')
    duplicate = program[program.index('if (!created)'):program.index('Application.EnableVisualStyles();')]
    assert 'if (!background)' in duplicate, 'Repeated background starts must not open the dashboard'
