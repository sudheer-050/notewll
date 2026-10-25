$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root '.speech-venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    python -m venv --system-site-packages (Join-Path $root '.speech-venv')
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the local speech environment.' }
}
& $python -c "import onnx_asr, onnxruntime, numpy, huggingface_hub"
if ($LASTEXITCODE -ne 0) {
    & $python -m pip install 'onnx-asr[cpu]==0.12.0' 'onnxruntime==1.23.2'
    if ($LASTEXITCODE -ne 0) { throw 'Accurate local speech dependencies could not be installed.' }
}
Write-Host 'Preparing the pinned Parakeet English model (about 2.5 GB if not already cached). Downloads happen only during setup.'
& $python -c "from huggingface_hub import snapshot_download; snapshot_download('istupakov/parakeet-tdt-0.6b-v2-onnx', revision='0bbb45a3365852604aef28b538a8f066f4ccaa85', allow_patterns=['config.json','vocab.txt','encoder-model.onnx','encoder-model.onnx.data','decoder_joint-model.onnx'])"
if ($LASTEXITCODE -ne 0) { throw 'The pinned local speech model could not be prepared.' }
& "$PSScriptRoot\build_setup.ps1"
& (Join-Path $root 'build\Notewll.exe') --accurate-batch $python | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Accurate speech readiness check failed. Existing app and preferences were not changed.' }
& "$PSScriptRoot\install_quiet_dictation.ps1" -QualityPython $python
Write-Host 'Accurate local dictation enabled. Wait for Ready in Notewll before using the shortcut.'
