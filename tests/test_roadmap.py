from test_app import run


def test_roadmap_preserves_sources_and_requires_review(tmp_path):
    result = run('--roadmap-smoke', str(tmp_path))
    assert result.returncode == 0, result.stderr
    assert 'encrypted-roundtrip|preview|tamper|private-export' in result.stdout
    from pypdf import PdfReader
    text = '\n'.join(page.extract_text() for page in PdfReader(tmp_path / 'redacted.pdf').pages)
    assert 'Alex' not in text
    assert 'keep secret' not in text
    assert '[REDACTED]' in text
