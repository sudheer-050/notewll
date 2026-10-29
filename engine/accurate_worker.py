"""Persistent, offline CPU speech recognition with a local neural speech gate.

No recording, transcript or model download is written during dictation.
The CPU provider is deliberate: measured CUDA cold inference took minutes here.
"""
import base64
import contextlib
import hashlib
import json
import os
from pathlib import Path
import sys
import time

MODEL_REVISION = '0bbb45a3365852604aef28b538a8f066f4ccaa85'
VAD_SHA256 = '1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3'
os.environ['HF_HUB_OFFLINE'] = '1'


def model_directory():
    return Path.home() / '.cache/huggingface/hub/models--istupakov--parakeet-tdt-0.6b-v2-onnx/snapshots' / MODEL_REVISION


class SpeechPresence:
    def __init__(self, model_path=None):
        import numpy as np
        import onnxruntime as ort
        self.np = np
        path = Path(model_path) if model_path else Path(__file__).with_name('silero_vad.onnx')
        if hashlib.sha256(path.read_bytes()).hexdigest() != VAD_SHA256:
            raise RuntimeError('The local speech detector failed its integrity check. Reinstall Notewll.')
        options = ort.SessionOptions()
        options.intra_op_num_threads = options.inter_op_num_threads = 1
        options.log_severity_level = 3
        self.session = ort.InferenceSession(str(path), sess_options=options, providers=['CPUExecutionProvider'])

    def inspect(self, audio):
        np = self.np
        state = np.zeros((2, 1, 128), dtype=np.float32)
        context = np.zeros((1, 64), dtype=np.float32)
        voiced = []
        for offset in range(0, len(audio), 512):
            block = audio[offset:offset + 512]
            if len(block) < 512:
                block = np.pad(block, (0, 512 - len(block)))
            block = block.reshape(1, 512)
            probability, state = self.session.run(None, {'input': np.concatenate((context, block), axis=1),
                                                       'state': state, 'sr': np.array(16000, dtype=np.int64)})
            context = block[:, -64:]
            if float(probability[0, 0]) >= 0.5:
                voiced.append(offset)
        speech_ms = len(voiced) * 32
        if speech_ms < 96:
            return {'speech_ms': speech_ms, 'start': 0, 'end': 0}
        # Preserve boundary context; do not concatenate separated words or mutate stored audio.
        return {'speech_ms': speech_ms, 'start': max(0, voiced[0] - 2560),
                'end': min(len(audio), voiced[-1] + 512 + 2560)}


class Recognizer:
    def __init__(self):
        import numpy as np
        import onnxruntime as ort
        import onnx_asr
        self.np = np
        directory = model_directory()
        for name in ['config.json', 'vocab.txt', 'encoder-model.onnx', 'encoder-model.onnx.data', 'decoder_joint-model.onnx']:
            if not (directory / name).is_file():
                raise RuntimeError('Set up accurate speech in Preferences > Storage > Optional features before dictating.')
        self.gate = SpeechPresence()
        options = ort.SessionOptions()
        options.intra_op_num_threads = 4
        options.inter_op_num_threads = 1
        options.log_severity_level = 3
        self.model = onnx_asr.load_model('nemo-parakeet-tdt-0.6b-v2', str(directory),
                                         sess_options=options, providers=['CPUExecutionProvider'])
        # Exercise the inference path before reporting ready; never expose warm-up output.
        self.model.recognize(np.zeros(16000, dtype=np.float32), sample_rate=16000)
        self.last_audio = self.last_text = None

    def run(self, request):
        if request.get('op') == 'warmup':
            return {'text': '', 'backend': 'Parakeet CPU + Silero speech detection', 'ready': True}
        started = time.perf_counter()
        pcm = base64.b64decode(request['pcm'], validate=True)
        if len(pcm) % 2:
            raise ValueError('Expected mono 16 kHz PCM16 audio.')
        audio = self.np.frombuffer(pcm, dtype='<i2').astype(self.np.float32) / 32768
        presence = self.gate.inspect(audio)
        if presence['end'] == 0:
            return {'text': '', 'speech_ms': presence['speech_ms'], 'cached': False,
                    'inference_ms': round((time.perf_counter() - started) * 1000)}
        clip = audio[presence['start']:presence['end']]
        signature = hashlib.sha256(clip.tobytes()).digest()
        cached = signature == self.last_audio
        if not cached:
            self.last_text = self.model.recognize(clip, sample_rate=16000).strip()
            self.last_audio = signature
        return {'text': self.last_text, 'speech_ms': presence['speech_ms'], 'cached': cached,
                'inference_ms': round((time.perf_counter() - started) * 1000)}


if __name__ == '__main__':
    worker = None
    for line in sys.stdin:
        try:
            with contextlib.redirect_stdout(sys.stderr):
                request = json.loads(line)
                if worker is None:
                    worker = Recognizer()
                result = worker.run(request)
        except Exception as exc:
            result = {'error': str(exc)}
        print(json.dumps(result), flush=True)
