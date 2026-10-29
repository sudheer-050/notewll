"""Local-only, persistent speech/voice worker. JSON lines on stdin/stdout."""
import base64
import contextlib
import json
import re
import sys


def safe_refinement(text, result):
    if not isinstance(result, str):
        return text
    # Permit punctuation/casing and literal fillers, never inferred words or altered facts.
    pattern = r"\d+(?:[.,]\d+)*|[^\W\d]+(?:['\u2019][^\W\d]+)*"
    fillers = {'um', 'uh', 'uhm', 'erm', 'hmm'}
    def words(value):
        return [word.casefold().replace('\u2019', "'") for word in re.findall(pattern, value)
                if word.casefold() not in fillers]
    return result.strip() if words(text) == words(result) else text


def refine(text):
    # Legacy worker operation: preserve recognized text; built-in C# rules handle grammar.
    return text


def vocabulary_prompt(values):
    if not isinstance(values, list):
        return None
    words = []
    for value in values[-24:]:
        if not isinstance(value, str) or not value.strip() or len(value) > 80 or any(ord(c) < 32 for c in value):
            continue
        if value.strip().casefold() not in [word.casefold() for word in words]:
            words.append(value.strip())
    # Leave decoder context for actual speech, including names with many subword tokens.
    text = ", ".join(words)
    return ("Vocabulary: " + text[:480] + ".") if text else None


class Worker:
    def __init__(self):
        self.model = self.encoder = None
        self.model_name = "small.en"

    def run(self, request):
        if request.get("op") == "refine":
            return {"text": refine(request["text"])}
        import numpy as np
        import torch
        import whisper
        torch.set_num_threads(4)
        language = request.get("language", "en")
        if language not in {"en", "auto", "es", "fr", "de", "it", "pt", "hi", "te", "ta", "ja", "ko", "zh", "ar"}:
            raise ValueError("Unsupported speech language")
        model_name = "small.en" if language == "en" else "base"
        if self.model is None or self.model_name != model_name:
            # Installed model required: never download during a recording.
            from pathlib import Path
            path = Path.home() / (".cache/whisper/" + model_name + ".pt")
            if not path.is_file():
                raise RuntimeError("Install the Whisper " + model_name + " model first")
            self.model = whisper.load_model(str(path), device="cpu")
            self.model_name = model_name
        audio = np.frombuffer(base64.b64decode(request["pcm"]), dtype="<i2").astype(np.float32) / 32768
        if len(audio) == 0 or float(np.sqrt(np.mean(audio ** 2))) < 0.002:
            return {"text": "", "turns": []}
        from resemblyzer import preprocess_wav
        if len(preprocess_wav(audio, source_sr=16000)) < 3200:
            return {"text": "", "turns": []}
        prompt = vocabulary_prompt(request.get("vocabulary"))
        if prompt:
            tokenizer = whisper.tokenizer.get_tokenizer(self.model.is_multilingual, language="en" if language == "auto" else language, task="transcribe")
            prompt = tokenizer.decode(tokenizer.encode(prompt)[:120])
        result = self.model.transcribe(audio, language=None if language == "auto" else language, task="transcribe", fp16=False, temperature=0,
                                       initial_prompt=prompt, carry_initial_prompt=True,
                                       condition_on_previous_text=False, word_timestamps=bool(request.get("voices")))
        turns = []
        if request.get("voices"):
            from resemblyzer import VoiceEncoder, preprocess_wav
            if self.encoder is None:
                self.encoder = VoiceEncoder(device="cpu")
            # Short windows detect voice changes within a transcription segment.
            for segment in result["segments"]:
                words = segment.get("words", [])
                groups = []
                for word in words:
                    if not groups or word["end"] - groups[-1][0]["start"] > 3:
                        groups.append([])
                    groups[-1].append(word)
                for group in groups:
                    start, end = group[0]["start"], group[-1]["end"]
                    clip = preprocess_wav(audio[int(start * 16000):int(end * 16000)], source_sr=16000)
                    embedding = self.encoder.embed_utterance(clip).tolist() if len(clip) >= 16000 else None
                    turns.append({"start": start, "end": end,
                                  "text": "".join(w["word"] for w in group).strip(), "embedding": embedding})
        if request.get("voices") and result["text"].strip() and not turns:
            turns.append({"start": 0, "end": len(audio) / 16000, "text": result["text"].strip(), "embedding": None})
        return {"text": result["text"].strip(), "turns": turns}


if __name__ == "__main__":
    worker = Worker()
    for line in sys.stdin:
        try:
            with contextlib.redirect_stdout(sys.stderr):
                result = worker.run(json.loads(line))
        except Exception as exc:
            result = {"error": str(exc)}
        print(json.dumps(result), flush=True)
