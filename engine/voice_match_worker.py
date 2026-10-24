"""Local speaker verification only. JSON lines; never saves or downloads audio/models."""
import base64
import contextlib
import json
import os
import sys
from pathlib import Path


class VoiceMatcher:
    def __init__(self):
        # Keep numerical-code caches in app data, not a read-only Python package folder.
        cache = Path(os.environ.get("NOTEWLL_VOICE_CACHE", str(Path(os.environ.get("LOCALAPPDATA", Path.home())) / "LocalDictation/VoiceMatching/cache")))
        cache.mkdir(parents=True, exist_ok=True)
        os.environ["NUMBA_CACHE_DIR"] = str(cache)
        import numpy as np
        import torch
        from resemblyzer import VoiceEncoder, preprocess_wav
        torch.set_num_threads(2)
        self.np, self.prepare = np, preprocess_wav
        self.encoder = VoiceEncoder(device="cpu", verbose=False)
        # Warm feature extraction before opening the always-on mic.
        self.encoder.embed_utterance(np.zeros(25600, dtype=np.float32))

    def audio(self, encoded, minimum):
        raw = base64.b64decode(encoded, validate=True)
        if len(raw) % 2 or len(raw) > 16 * 32000:
            raise ValueError("Use a short 16 kHz mono PCM voice sample.")
        wav = self.np.frombuffer(raw, dtype="<i2").astype(self.np.float32) / 32768
        if not len(wav) or float(self.np.sqrt(self.np.mean(wav ** 2))) < .003:
            raise ValueError("Not enough clear speech. Speak a longer phrase in a quiet room.")
        if self.np.mean(self.np.abs(wav) >= .99) > .03:
            raise ValueError("The sample is empty or clipped. Speak at a comfortable level.")
        wav = self.prepare(wav)  # Already fixed at 16 kHz; do not resample it.
        if len(wav) < minimum * 16000:
            raise ValueError("Not enough clear speech. Speak a longer phrase in a quiet room.")
        return wav

    def profile(self, values):
        refs = self.np.asarray(values, dtype=self.np.float32)
        if refs.shape != (3, 256) or not self.np.isfinite(refs).all():
            raise ValueError("Enroll your voice again: invalid voice profile.")
        norms = self.np.linalg.norm(refs, axis=1)
        if (norms < .9).any() or (norms > 1.1).any():
            raise ValueError("Enroll your voice again: invalid voice profile.")
        return refs / norms[:, None]

    def run(self, request):
        if request.get("op") == "ready":
            return {"ready": True}
        if request.get("op") == "enroll":
            clips = request.get("clips", [])
            if len(clips) != 3:
                raise ValueError("Record three voice samples.")
            refs = self.np.stack([self.encoder.embed_utterance(self.audio(clip, 2.0)) for clip in clips])
            if min(float(refs[i] @ refs[j]) for i in range(3) for j in range(i)) < .80:
                raise ValueError("The samples do not match consistently. Record only your voice in a quiet room.")
            return {"ready": True, "references": refs.tolist()}
        if request.get("op") != "verify":
            raise ValueError("Unknown voice matching request.")
        refs = self.profile(request.get("references"))
        try:
            wav = self.audio(request["pcm"], 1.4)
        except ValueError:
            return {"accepted": False, "score": 0.0}
        # Three-second overlapping windows reduce phoneme-sensitive false rejections
        # from comparing each individual 1.6-second model partial to enrollment.
        size = min(len(wav), 48000)
        starts = list(range(0, max(1, len(wav) - size + 1), 16000))
        if starts[-1] != len(wav) - size:
            starts.append(len(wav) - size)
        parts = self.np.stack([self.encoder.embed_utterance(wav[start:start + size]) for start in starts])
        center = refs.mean(axis=0)
        center /= self.np.linalg.norm(center)
        score = float((parts @ center).min())
        reference_floor = float((parts @ refs.T).min())
        # Conservative experimental thresholds, not a biometric security guarantee.
        accepted = score >= .86 and reference_floor >= .80
        return {"accepted": bool(accepted), "score": score}


def main():
    matcher = None
    for line in sys.stdin:
        try:
            if len(line) > 3000000:
                raise ValueError("Voice request too large.")
            with contextlib.redirect_stdout(sys.stderr):
                if matcher is None:
                    matcher = VoiceMatcher()
                result = matcher.run(json.loads(line))
        except Exception as exc:
            result = {"accepted": False, "error": str(exc)}
        print(json.dumps(result, allow_nan=False), flush=True)


if __name__ == "__main__":
    main()
