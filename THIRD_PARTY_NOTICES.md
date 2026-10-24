ï»¿# Third-party notices

The Windows app integrates Vosk API 0.3.45 and vosk-model-small-en-us-0.15 by Alpha Cephei directly through the native C API. Both are distributed under Apache License 2.0.

Vosk API and license: https://github.com/alphacep/vosk-api/tree/v0.3.45
English model, source and model license: https://alphacephei.com/vosk/models
Apache License 2.0: https://www.apache.org/licenses/LICENSE-2.0

The Vosk Windows binary distribution includes the GCC runtime libraries libgcc_s_seh-1.dll, libstdc++-6.dll and libwinpthread-1.dll. Their upstream license and runtime library exception apply; these are unmodified runtime components distributed by the Vosk project. Source: https://github.com/alphacep/vosk-api/releases/tag/v0.3.45
GCC Runtime Library Exception: https://www.gnu.org/licenses/gcc-exception-3.1.html
MinGW runtime source and license notices: https://www.mingw-w64.org/

The single-file installer includes the native Vosk runtime and small English model. Optional accurate dictation uses a separately installed Python environment, onnx-asr and ONNX Runtime on CPU, with separately downloaded NVIDIA Parakeet TDT 0.6B v2 weights (CC BY 4.0). It does not package Holly, Docker, CUDA or the Parakeet model weights. Recognition stays local after setup.

Parakeet model card and license: https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2
ONNX conversion: https://huggingface.co/istupakov/parakeet-tdt-0.6b-v2-onnx
onnx-asr (MIT): https://github.com/istupakov/onnx-asr
ONNX Runtime (MIT): https://github.com/microsoft/onnxruntime

The installer includes an unmodified Silero VAD ONNX model for local speech detection. Source: https://github.com/snakers4/silero-vad/blob/master/src/silero_vad/data/silero_vad.onnx
Git blob SHA: 80c5592ef1f4c9ede3e357bbd02eb863358a6a9d
SHA256: 1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3

MIT License

Copyright (c) 2020-present Silero Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
