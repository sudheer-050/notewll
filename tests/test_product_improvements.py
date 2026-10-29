from test_app import run


def test_product_backend_preserves_rules_formats_and_failed_saves(tmp_path):
    result = run('--product-smoke', str(tmp_path))
    assert result.returncode == 0, result.stderr
    assert 'simultaneous-rules|longest-match|identifiers|raw-preserved' in result.stdout


def test_multilingual_worker_routes_language_without_translation_or_download(monkeypatch):
    import base64
    import importlib.util
    import sys
    from pathlib import Path
    from types import SimpleNamespace
    import pytest
    np = pytest.importorskip('numpy')
    path = Path(__file__).parents[1] / 'engine/quality_worker.py'
    spec = importlib.util.spec_from_file_location('multilingual_worker_test', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    calls = []
    class Model:
        def transcribe(self, audio, **options):
            calls.append(options)
            return {'text': 'Hola, Alex.', 'segments': []}
    monkeypatch.setitem(sys.modules, 'torch', SimpleNamespace(set_num_threads=lambda count: None))
    monkeypatch.setitem(sys.modules, 'whisper', SimpleNamespace())
    monkeypatch.setitem(sys.modules, 'resemblyzer', SimpleNamespace(preprocess_wav=lambda audio, **kw: audio))
    worker = module.Worker()
    worker.model, worker.model_name = Model(), 'base'
    pcm = base64.b64encode(np.full(16000, 1000, dtype='<i2').tobytes()).decode()
    assert worker.run({'language': 'es', 'pcm': pcm})['text'] == 'Hola, Alex.'
    worker.run({'language': 'auto', 'pcm': pcm})
    assert calls[0]['language'] == 'es' and calls[1]['language'] is None
    assert all(call['task'] == 'transcribe' for call in calls)
    silence = base64.b64encode(np.zeros(16000, dtype='<i2').tobytes()).decode()
    assert worker.run({'language': 'es', 'pcm': silence})['text'] == ''
    assert len(calls) == 2
    with pytest.raises(ValueError, match='Unsupported'):
        worker.run({'language': 'invalid', 'pcm': pcm})
