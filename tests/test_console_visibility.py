"""Exercise Holly's subprocess callers without loading its speech models."""
import ast
import io
import subprocess
import tempfile
from pathlib import Path
from types import SimpleNamespace

from flask import Flask, jsonify, request


def test_transcription_and_gpu_probe_request_hidden_console_windows(tmp_path, monkeypatch):
    backend = Path(__file__).parents[2] / "ollama-personal-model" / "holly_server.py"
    tree = ast.parse(backend.read_text(encoding="utf-8"))
    functions = [node for node in tree.body if isinstance(node, ast.FunctionDef)
                 and node.name in {"transcribe", "_free_vram_mib"}]
    calls = []

    def run(command, **options):
        calls.append((command, options))
        assert options["creationflags"] == subprocess.CREATE_NO_WINDOW
        return SimpleNamespace(stdout="4096\n")

    original_temp = tempfile.NamedTemporaryFile
    app = Flask(__name__)
    context = {"app": app, "request": request, "jsonify": jsonify, "Path": Path,
               "subprocess": SimpleNamespace(run=run, CREATE_NO_WINDOW=subprocess.CREATE_NO_WINDOW),
               "tempfile": SimpleNamespace(NamedTemporaryFile=lambda **kw: original_temp(dir=tmp_path, **kw)),
               "transcribe_with_fallback": lambda path: "Speech was transcribed.",
               "detect_emotion": lambda path: None}
    exec(compile(ast.Module(body=functions, type_ignores=[]), str(backend), "exec"), context)
    assert context["_free_vram_mib"]() == 4096
    with app.test_request_context("/api/transcribe", method="POST",
                                  data={"audio": (io.BytesIO(b"encoded test audio"), "audio.wav")}):
        assert context["transcribe"]().get_json()["text"] == "Speech was transcribed."
    assert [call[0][0] for call in calls] == ["nvidia-smi", "ffmpeg"]
    assert list(tmp_path.iterdir()) == []
