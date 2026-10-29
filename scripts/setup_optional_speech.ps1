param(
    [string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'LocalDictation'),
    [ValidateSet('Accurate', 'Enhanced', 'Multilingual')][string]$Feature = 'Accurate',
    [switch]$IncludeAccurate,
    [switch]$IncludeEnhanced
)
$ErrorActionPreference = 'Stop'
$runtime = Join-Path $DataRoot 'SpeechRuntime'
$python = Join-Path $runtime 'Scripts\python.exe'
if (!(Test-Path -LiteralPath $python)) {
    if (Get-Command py -ErrorAction SilentlyContinue) { & py -3 -m venv $runtime }
    elseif (Get-Command python -ErrorAction SilentlyContinue) { & python -m venv $runtime }
    else { throw 'Install Python 3.10 or later, then run optional speech setup again. Standard English speech works without Python.' }
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the optional speech environment.' }
}
if ($Feature -eq 'Accurate' -or $IncludeAccurate) {
    & $python -m pip install 'onnx-asr[cpu]==0.12.0' 'onnxruntime==1.23.2'
    if ($LASTEXITCODE -ne 0) { throw 'Accurate speech dependencies could not be installed.' }
    Write-Output 'Preparing the pinned English speech model (about 2.5 GB if not cached).'
    & $python -c "from huggingface_hub import snapshot_download; snapshot_download('istupakov/parakeet-tdt-0.6b-v2-onnx', revision='0bbb45a3365852604aef28b538a8f066f4ccaa85', allow_patterns=['config.json','vocab.txt','encoder-model.onnx','encoder-model.onnx.data','decoder_joint-model.onnx'])"
    if ($LASTEXITCODE -ne 0) { throw 'The accurate English speech model could not be prepared.' }
}
if ($Feature -eq 'Enhanced' -or $IncludeEnhanced) {
    & $python -m pip install 'setuptools>=77.0.3,<81' openai-whisper 'resemblyzer==0.1.4'
    if ($LASTEXITCODE -ne 0) { throw 'Enhanced meeting speech dependencies could not be installed.' }
    & $python -c "import whisper; whisper.load_model('small.en', device='cpu'); from resemblyzer import VoiceEncoder; VoiceEncoder(device='cpu', verbose=False)"
    if ($LASTEXITCODE -ne 0) { throw 'Enhanced meeting speech models could not be prepared.' }
}
if ($Feature -eq 'Multilingual') {
    & $python -m pip install 'setuptools>=77.0.3,<81' openai-whisper 'resemblyzer==0.1.4'
    if ($LASTEXITCODE -ne 0) { throw 'Multilingual speech dependencies could not be installed.' }
    & $python -c "import whisper; whisper.load_model('base', device='cpu')"
    if ($LASTEXITCODE -ne 0) { throw 'The local multilingual model could not be prepared.' }
}
Write-Output 'Optional speech is ready. Enable the feature in Preferences > Writing, then save.'
