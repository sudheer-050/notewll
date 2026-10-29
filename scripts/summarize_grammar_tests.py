"""Summarize the local screening results without treating missing runs as passes."""
import json
import hashlib
import re
import statistics
from pathlib import Path
from check_tiny_grammar import ROOT, words


def summarize():
    root = ROOT / 'output' / 'grammar-comparison'
    root.mkdir(parents=True, exist_ok=True)
    suite = json.loads((ROOT / 'verification/grammar_cases.json').read_text(encoding='utf-8'))
    expected_inputs = {r['raw'] for r in suite}
    entries = [('Built-in rules', root / 'builtin-results.json', 0)]
    for candidate in ['tiny', 'unbabel']:
        for decoding in ['greedy', 'beam5']:
            folder = root / (candidate + '-' + decoding)
            entries.append((candidate + ' ' + decoding, folder / 'result.json', None))
            entries.append((candidate + ' ' + decoding + ' + current guard', folder / 'guarded-results.json', None))
    summary = []
    for name, path, size in entries:
        entry = dict(name=name, status='not_completed', source=str(path))
        if path.exists():
            data = json.loads(path.read_text(encoding='utf-8-sig'))
            if isinstance(data, list) and size is None and path.with_name('result.json').exists():
                size = json.loads(path.with_name('result.json').read_text(encoding='utf-8-sig')).get('asset_bytes')
            rows = data if isinstance(data, list) else data.get('cases', [])
            if len(rows) == len(suite) and {r['raw'] for r in rows} == expected_inputs:
                counts = {kind: sum(words(r['actual']) == words(r['reference']) for r in rows if r['kind'] == kind) for kind in ['grammar', 'preserve']}
                entry.update(status='completed', grammar_reference_matches=counts['grammar'],
                             grammar_cases=60, preserved_controls=counts['preserve'], control_cases=60,
                             changed_controls=60 - counts['preserve'],
                             median_ms=round(statistics.median(r['ms'] for r in rows), 2),
                             model_bytes=size if isinstance(data, list) else data.get('asset_bytes'),
                             failures=[r for r in rows if words(r['actual']) != words(r['reference'])],
                             exact_reference_matches=sum(r['actual'] == r['reference'] for r in rows),
                             numeric_changes=[dict(raw=r['raw'], actual=r['actual']) for r in rows
                                 if re.findall(r'\d+(?:[.,:]\d+)*', r['raw']) != re.findall(r'\d+(?:[.,:]\d+)*', r['actual'])],
                             truncated=sum(r.get('truncated', False) for r in rows))
            else:
                entry['error'] = 'Missing or incomplete current 120-case suite.'
        summary.append(entry)
    result = dict(suite_cases=len(suite), suite_sha256=hashlib.sha256((ROOT / 'verification/grammar_cases.json').read_bytes()).hexdigest(), independent_human_review=False,
                  note='Author-written references and greedy/custom beam screening. Reference word matches are not a general grammar accuracy percentage. Case/punctuation ignored in word scores; valid alternate corrections need review.',
                  runs=summary)
    (root / 'summary.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    lines = ['# Offline grammar screening', '', '120 author-written sentences: 60 grammar cases and 60 correct-text controls. This is not a representative accuracy benchmark. Reference matches ignore case and punctuation. Some reference mismatches can be valid alternatives; changed controls still require review.', '',
             '| Method | Grammar reference matches | Correct controls unchanged | Median inference |', '|---|---:|---:|---:|']
    for row in summary:
        if row['status'] == 'completed':
            lines.append(f"| {row['name']} | {row['grammar_reference_matches']}/60 | {row['preserved_controls']}/60 | {row['median_ms']} ms |")
        else:
            lines.append(f"| {row['name']} | Not completed | Not completed | Not measured |")
    lines += ['', 'Cached model inference ran locally with CPUExecutionProvider. These timings exclude recognition, initial loading, desktop insertion and user review. Guard timings are inherited from their model run, not separately timed.', '',
              'Tiny model assets: 34,842,310 bytes (33.23 MiB). Unbabel selected assets: 97,166,925 bytes (92.67 MiB). These exclude runtime and tokenizer implementation. The installed ONNX runtime CPU DLL alone is 15,551,032 bytes; Python bindings and optional CUDA DLLs are not required measurements for a future native build. There is no measured final package size.', '',
              ('The larger Unbabel model has completed runs; review them alongside the tiny model.' if any(r['status'] == 'completed' and r['name'].startswith('unbabel') for r in summary) else 'The larger Unbabel model is pending until downloaded and tested.') + ' No evaluated method establishes correction of every error or preservation of every fact.', '',
              'The custom five-beam search is an exploratory comparison, not the exact Transformers generation implementation. No learned model or rule was fitted to these references.', '']
    for row in summary:
        if row['status'] != 'completed' or 'current guard' in row['name']:
            continue
        lines.extend(['## ' + row['name'] + ' - unmatched cases', ''])
        for case in row['failures']:
            lines.append('- ' + case['kind'] + ': ' + json.dumps(case['raw']) + ' → ' + json.dumps(case['actual']) + ' | Reference: ' + json.dumps(case['reference']))
        lines.append('')
    (root / 'SUMMARY.md').write_text('\n'.join(lines), encoding='utf-8')
    for row in summary:
        print(row['name'], row['status'], f"grammar {row.get('grammar_reference_matches', '-')}/60; unchanged {row.get('preserved_controls', '-')}/60")


if __name__ == '__main__':
    summarize()
