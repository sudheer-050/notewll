"""Offline-only passage retrieval. Setup is a separate, explicit operation."""
import json
import os
import sys
import time
from pathlib import Path


def search(request):
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    import numpy as np
    from sentence_transformers import SentenceTransformer
    root = Path(request['root'])
    started = time.perf_counter()
    model = SentenceTransformer(str(root / 'model'), device='cpu', local_files_only=True)
    # Bound individual chunks to prevent long passages hiding relevant sentences.
    chunks, owners = [], []
    for i, text in enumerate(request['texts']):
        words = text.split()
        for start in range(0, len(words), 96):
            chunks.append(' '.join(words[start:start + 128]))
            owners.append(i)
    if not chunks:
        return {'hits': []}
    matrix = model.encode(chunks, batch_size=32, normalize_embeddings=True, show_progress_bar=False)
    query = model.encode([request['query']], normalize_embeddings=True, show_progress_bar=False)[0]
    scores = matrix @ query
    best = {}
    for owner, score in zip(owners, scores):
        best[owner] = max(best.get(owner, -1.0), float(score))
    import ctypes
    from ctypes import wintypes
    class Memory(ctypes.Structure):
        _fields_ = [('cb', wintypes.DWORD), ('faults', wintypes.DWORD), ('peak', ctypes.c_size_t), ('working', ctypes.c_size_t), ('pool_peak', ctypes.c_size_t), ('pool', ctypes.c_size_t), ('non_peak', ctypes.c_size_t), ('non', ctypes.c_size_t), ('page', ctypes.c_size_t), ('page_peak', ctypes.c_size_t)]
    usage = Memory(); usage.cb = ctypes.sizeof(usage)
    ctypes.windll.kernel32.GetCurrentProcess.restype = wintypes.HANDLE
    if not ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.windll.kernel32.GetCurrentProcess(), ctypes.byref(usage), usage.cb):
        raise OSError('Cannot measure semantic worker memory')
    metrics = {'worker_peak_working_set_bytes': usage.peak, 'worker_working_set_bytes': usage.working, 'elapsed_seconds': time.perf_counter() - started, 'passages': len(request['texts']), 'chunks': len(chunks)}
    (root / 'last-measurement.json').write_text(json.dumps(metrics), encoding='utf-8')
    return {'hits': [{'index': i, 'score': score} for i, score in sorted(best.items(), key=lambda p: -p[1])[:20] if score >= .25]}


if __name__ == '__main__':
    try:
        request = json.loads(sys.stdin.read())
        print(json.dumps(search(request)))
    except Exception as exc:
        print(json.dumps({'error': str(exc)}))
        sys.exit(1)
