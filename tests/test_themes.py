import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_theme_selection_previews_without_writing_and_survives_restart(tmp_path):
    result = subprocess.run([str(ROOT / 'build/Notewll.exe'), '--themes-smoke', str(tmp_path)], capture_output=True, text=True, timeout=45)
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'themes-ok|choices=8|preview-without-saving|save-reload|readable-colors|legacy-fallback'
