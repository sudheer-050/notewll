import importlib.util
import json
import os
import zipfile
from pathlib import Path

import pytest

from test_app import ROOT, run, write_config


def engine():
    root = Path(os.environ.get('LOCALAPPDATA', '')) / 'Programs/LocalDictation'
    if not (root / 'native/libvosk.dll').is_file():
        pytest.skip('Installed native engine unavailable')
    return str(root)


def test_review_preserves_words_and_respects_export_scope(tmp_path):
    result = run('--review-smoke', str(tmp_path))
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'review-ok|speaker-metadata|words-unchanged|export-scope|sort|preferences'


def test_concurrent_native_shutdown_releases_all_streams():
    result = run('--engine-stress', engine())
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'stress-ok|16-streams|shutdown|released|new-stream-rejected'


def test_continuous_native_notes_leave_silence_empty(tmp_path):
    result = run('--native-notes-smoke', engine(), str(tmp_path))
    assert result.returncode == 0, result.stderr
    assert result.stdout == 'notes-ok|both-sources|continuous|silence-empty|released'


@pytest.mark.parametrize('field,value', [('NotesSort', 'random'), ('ExportFormat', 'executable')])
def test_review_preferences_reject_invalid_values(tmp_path, field, value):
    result = run('--validate-config', write_config(tmp_path, **{field: value}))
    assert result.returncode == 2


def test_import_validates_every_record_before_moving_any(tmp_path):
    seed = tmp_path / 'seed'
    assert run('--review-smoke', str(seed)).returncode == 0
    notes = list(seed.glob('*/note.json'))
    valid = json.loads(notes[0].read_text())
    invalid = json.loads(notes[1].read_text())
    invalid['Segments'] = [{'Source': 'Computer', 'StartFrame': -1, 'Frames': 1}]
    archive = tmp_path / 'invalid.zip'
    with zipfile.ZipFile(archive, 'w') as output:
        for note in (valid, invalid):
            output.writestr(note['Id'] + '/note.json', json.dumps(note))
    root = tmp_path / 'restored'
    result = run('--restore-backup', str(root), str(archive))
    assert result.returncode != 0
    assert not list(root.glob('*/note.json'))


def test_word_error_benchmark_counts_insertions_deletions_substitutions():
    spec = importlib.util.spec_from_file_location('benchmark_speech', ROOT / 'scripts/benchmark_speech.py')
    benchmark = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(benchmark)
    assert benchmark.word_errors('Hello, Alex!', 'hello alex') == (0, 2)
    assert benchmark.word_errors('send the report', 'send a final report') == (2, 3)
    assert benchmark.word_errors('', 'noise words') == (2, 0)
    assert benchmark.word_errors('all words', '') == (2, 2)


def test_update_rejects_unsigned_installer_and_malformed_manifests(tmp_path):
    result = run('--verify-update-signature', str(ROOT / 'build/Notewll.exe'), '0' * 40)
    assert result.returncode == 0 and result.stdout == 'invalid'
    for values in ({'Version': '0.7.0', 'InstallerUrl': 'http://example.com/setup.exe', 'Sha256': 'a' * 64},
                   {'Version': 'bad', 'InstallerUrl': 'https://example.com/setup.exe', 'Sha256': 'a' * 64},
                   {'Version': '0.7.0', 'InstallerUrl': 'https://example.com/setup.exe', 'Sha256': 'short'}):
        manifest = tmp_path / 'update.json'
        manifest.write_text(json.dumps(values))
        assert run('--validate-update', str(manifest)).returncode != 0
    manifest.write_text(json.dumps({'Version': '0.7.0', 'InstallerUrl': 'https://example.com/setup.exe', 'Sha256': 'a' * 64}))
    assert run('--validate-update', str(manifest)).stdout == 'valid'
