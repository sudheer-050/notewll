"""Evaluate T5 Tiny GEC in isolation. Never changes Notewll or its settings.

First run downloads only quantized model assets; --offline reuses them.
Results are synthetic screening evidence, not a general accuracy benchmark.
"""
import argparse
import hashlib
import json
import re
import time
from pathlib import Path

MODEL = 'rabden/t5-tiny-gec-hone'
ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / 'output' / 'tiny-grammar-evaluation'
CASES = [
    ('grammar', 'he go to school yesterday', 'He went to school yesterday.'),
    ('grammar', 'she dont like apples', "She doesn't like apples."),
    ('grammar', 'they was running fast', 'They were running fast.'),
    ('grammar', 'I has a meeting tomorrow', 'I have a meeting tomorrow.'),
    ('grammar', 'She have a report', 'She has a report.'),
    ('grammar', 'They needs help', 'They need help.'),
    ('grammar', 'I am agree with you', 'I agree with you.'),
    ('grammar', 'He did not went to school', 'He did not go to school.'),
    ('grammar', 'She can sings well', 'She can sing well.'),
    ('grammar', 'I have two childs', 'I have two children.'),
    ('grammar', 'This are my books', 'These are my books.'),
    ('grammar', 'I bought a apple', 'I bought an apple.'),
    ('grammar', 'We have lived here since five years', 'We have lived here for five years.'),
    ('grammar', 'I look forward to meet you', 'I look forward to meeting you.'),
    ('grammar', 'Yesterday I go to office', 'Yesterday I went to the office.'),
    ('preserve', 'Do not send it.', 'Do not send it.'),
    ('preserve', "I don't agree.", "I don't agree."),
    ('preserve', 'Send it at 3, not by 3.', 'Send it at 3, not by 3.'),
    ('preserve', 'Pay 15.50 to Sudheer on Monday.', 'Pay 15.50 to Sudheer on Monday.'),
    ('preserve', 'Call Alex, then Sam.', 'Call Alex, then Sam.'),
    ('preserve', 'Only send one copy.', 'Only send one copy.'),
    ('preserve', 'Wait until Friday.', 'Wait until Friday.'),
    ('preserve', 'If I were ready, I would go.', 'If I were ready, I would go.'),
    ('preserve', 'She had had enough.', 'She had had enough.'),
    ('preserve', 'He said that that was fine.', 'He said that that was fine.'),
    ('preserve', 'I paid 15 and 15.', 'I paid 15 and 15.'),
    ('preserve', 'We need the report and invoice.', 'We need the report and invoice.'),
    ('preserve', 'I have gone to the store.', 'I have gone to the store.'),
    ('preserve', 'The meeting is on 24 October 2026.', 'The meeting is on 24 October 2026.'),
    ('preserve', 'I can send it, but I will not.', 'I can send it, but I will not.'),
]


def words(text):
    return re.findall(r"\d+(?:[.,]\d+)*|[^\W\d]+(?:['’][^\W\d]+)*", text.lower().replace('’', "'"))


def main():
    global MODEL, OUTPUT_DIR
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--offline', action='store_true')
    parser.add_argument('--self-check', action='store_true')
    parser.add_argument('--candidate', choices=['tiny', 'unbabel'], default='tiny')
    parser.add_argument('--suite', type=Path, help='JSON list of kind/raw/reference test cases')
    parser.add_argument('--output', type=Path, help='Separate output folder for a comparison run')
    parser.add_argument('--beams', type=int, choices=[1, 5], default=1)
    args = parser.parse_args()
    if args.candidate == 'unbabel':
        MODEL = 'TonyRaju/gec-t5-small-coedit-onnx-int8'
        OUTPUT_DIR = ROOT / 'output' / 'unbabel-grammar-evaluation'
    if args.output:
        OUTPUT_DIR = args.output.resolve()
    cases = CASES if not args.suite else [(r['kind'], r['raw'], r['reference']) for r in json.loads(args.suite.read_text(encoding='utf-8'))]
    encoder_file = 'encoder_model.onnx' if args.candidate == 'unbabel' else 'onnx/encoder_model_quantized.onnx'
    decoder_file = 'decoder_model.onnx' if args.candidate == 'unbabel' else 'onnx/decoder_model_merged_quantized.onnx'
    if args.self_check:
        assert len(CASES) == 30
        assert words('Pay 15.50 to Sudheer.') != words('Pay 1550 to Sudheer.')
        assert words('Do not send it.') != words('Do send it.')
        assert words('He said that that was fine.') != words('He said that was fine.')
        print('Evaluation harness self-check passed; model inference has not run.')
        return

    import numpy as np
    import onnxruntime as ort
    from huggingface_hub import snapshot_download
    from transformers import AutoTokenizer

    out = OUTPUT_DIR
    out.mkdir(parents=True, exist_ok=True)
    result_path = out / 'result.json'
    result_path.write_text(json.dumps({'status': 'not_completed', 'model': MODEL}), encoding='utf-8')
    print('Preparing quantized assets (first run needs internet)...', flush=True)
    cache = ROOT / 'output' / ('unbabel-grammar-evaluation' if args.candidate == 'unbabel' else 'tiny-grammar-evaluation') / 'model-cache'
    folder = Path(snapshot_download(MODEL, cache_dir=str(cache), local_files_only=args.offline,
        allow_patterns=['config.json', 'generation_config.json', 'tokenizer.json', 'tokenizer_config.json',
                        'special_tokens_map.json', 'spiece.model', encoder_file, decoder_file]))
    config = json.loads((folder / 'config.json').read_text())
    tokenizer = AutoTokenizer.from_pretrained(str(folder), local_files_only=True, trust_remote_code=False)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    start = time.perf_counter()
    encoder = ort.InferenceSession(str(folder / encoder_file), options, providers=['CPUExecutionProvider'])
    decoder = ort.InferenceSession(str(folder / decoder_file), options, providers=['CPUExecutionProvider'])
    load_ms = round((time.perf_counter() - start) * 1000, 1)
    metadata = [{'name': item.name, 'shape': item.shape, 'type': item.type} for item in decoder.get_inputs()]
    (out / 'decoder-inputs.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')

    def correct(text):
        encoded = tokenizer(('gec: ' if args.candidate == 'unbabel' else '') + text, return_tensors='np')
        encoder_feed = {i.name: np.asarray(encoded[i.name], dtype=np.int64) for i in encoder.get_inputs()}
        hidden = encoder.run(None, encoder_feed)[0]
        ids = [config.get('decoder_start_token_id', config.get('pad_token_id', 0))]
        # Use the non-cache branch with the full prefix each step. Slower but avoids
        # depending on this export's custom cross-attention cache implementation.
        def logits_for(prefix):
            feed = {}
            for item in decoder.get_inputs():
                if item.name in ('input_ids', 'decoder_input_ids'):
                    feed[item.name] = np.array([prefix], dtype=np.int64)
                elif item.name == 'encoder_hidden_states':
                    feed[item.name] = hidden
                elif item.name in ('encoder_attention_mask', 'attention_mask'):
                    feed[item.name] = np.asarray(encoded['attention_mask'], dtype=np.int64)
                elif item.name == 'use_cache_branch':
                    feed[item.name] = np.array([False], dtype=np.bool_)
                elif item.name.startswith('past_key_values'):
                    feed[item.name] = np.zeros((1, config['num_heads'], 0, config['d_kv']), dtype=np.float32)
                else:
                    raise RuntimeError('Unsupported decoder input: ' + item.name)
            return decoder.run(['logits'], feed)[0][0, -1]

        if args.beams > 1:
            beams = [(ids, 0.0, False)]
            for step in range(96):
                expanded = []
                for prefix, score, done in beams:
                    if done:
                        expanded.append((prefix, score, True))
                        continue
                    logits = logits_for(prefix).astype(np.float64)
                    normalizer = np.max(logits)
                    probabilities = logits - normalizer - np.log(np.exp(logits - normalizer).sum())
                    for token in np.argsort(probabilities)[-args.beams:]:
                        expanded.append((prefix + [int(token)], score + float(probabilities[token]), int(token) == config.get('eos_token_id', 1)))
                beams = sorted(expanded, key=lambda row: row[1] / max(1, len(row[0]) - 1), reverse=True)[:args.beams]
                if all(row[2] for row in beams):
                    break
            best = max(beams, key=lambda row: row[1] / max(1, len(row[0]) - 1))
            return tokenizer.decode(best[0][1:], skip_special_tokens=True), not best[2]

        for _ in range(96):
            logits = logits_for(ids)
            token = int(logits.argmax())
            if token == config.get('eos_token_id', 1):
                return tokenizer.decode(ids[1:], skip_special_tokens=True), False
            ids.append(token)
        return tokenizer.decode(ids[1:], skip_special_tokens=True), True

    rows = []
    for kind, raw, expected in cases:
        start = time.perf_counter()
        actual, truncated = correct(raw)
        row = dict(kind=kind, raw=raw, reference=expected, actual=actual, truncated=truncated,
                   reference_words_match=words(actual) == words(expected), ms=round((time.perf_counter() - start) * 1000, 1))
        rows.append(row)
        print(json.dumps(row), flush=True)
        # Keep partial evidence if a later case fails.
        (out / 'partial-results.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')

    assets = []
    for path in folder.rglob('*'):
        if path.is_file() and path.suffix in ('.onnx', '.json', '.model'):
            assets.append(dict(file=str(path.relative_to(folder)), bytes=path.stat().st_size,
                               sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    report = dict(status='completed', model=MODEL, revision=folder.name, offline=args.offline, beams=args.beams,
                  onnxruntime=ort.__version__, load_ms=load_ms, assets=assets,
                  asset_bytes=sum(a['bytes'] for a in assets), cases=rows,
                  grammar_reference_matches=sum(r['reference_words_match'] for r in rows if r['kind'] == 'grammar'),
                  unchanged_preservation_cases=sum(r['reference_words_match'] for r in rows if r['kind'] == 'preserve'),
                  grammar_cases=sum(r['kind'] == 'grammar' for r in rows),
                  preservation_cases=sum(r['kind'] == 'preserve' for r in rows),
                  note='Synthetic screening cases, not a representative or independently reviewed accuracy benchmark. Word matches ignore punctuation and case. Review alternatives and factual changes. CPU timings use full-prefix decoding; beam search here uses a simple length-normalized score, not an exact reproduction of Transformers beam search.')
    result_path.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('Saved: ' + str(result_path))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        out = OUTPUT_DIR
        out.mkdir(parents=True, exist_ok=True)
        (out / 'result.json').write_text(json.dumps(dict(status='not_completed', model=MODEL,
            error=str(error), model_inference_verified=False), indent=2), encoding='utf-8')
        raise
