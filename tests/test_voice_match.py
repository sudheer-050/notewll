import importlib.util
from pathlib import Path

import numpy as np
import pytest

spec = importlib.util.spec_from_file_location("voice_match", Path(__file__).parents[1] / "engine/voice_match_worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


def matcher(parts):
    instance = worker.VoiceMatcher.__new__(worker.VoiceMatcher)
    instance.np = np
    instance.audio = lambda encoded, minimum: np.ones(96000, dtype=np.float32)

    class Encoder:
        index = 0
        def embed_utterance(self, wav, **kwargs):
            result = parts[self.index % len(parts)]
            self.index += 1
            return result

    instance.encoder = Encoder()
    return instance


def basis(index):
    v = np.zeros(256, dtype=np.float32)
    v[index] = 1
    return v


def request():
    return {"op": "verify", "references": [basis(0).tolist()] * 3, "pcm": "unused"}


def test_enrolled_voice_passes_all_windows():
    assert matcher(np.stack([basis(0)] * 4)).run(request())["accepted"] is True


def test_different_speaker_is_rejected():
    assert matcher(np.stack([basis(1)] * 4)).run(request())["accepted"] is False


def test_one_unknown_window_rejects_whole_phrase():
    assert matcher(np.stack([basis(0), basis(0), basis(1)])).run(request())["accepted"] is False


def test_no_profile_or_invalid_vectors_fail_closed():
    for refs in (None, [], [[0] * 256] * 3, [[float("nan")] * 256] * 3):
        req = request()
        req["references"] = refs
        with pytest.raises(ValueError):
            matcher(np.stack([basis(0)])).run(req)


def test_short_or_unclear_speech_is_rejected():
    instance = matcher(np.stack([basis(0)]))
    instance.audio = lambda *args: (_ for _ in ()).throw(ValueError("not enough speech"))
    assert instance.run(request()) == {"accepted": False, "score": 0.0}


def test_nan_inference_does_not_accept():
    assert matcher(np.full((2, 256), np.nan)).run(request())["accepted"] is False
