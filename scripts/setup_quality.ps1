$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$python = Join-Path $root '.speech-venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $python)) { python -m venv --system-site-packages (Join-Path $root '.speech-venv') }
& $python -c "import whisper; from resemblyzer import VoiceEncoder" 2>$null
if ($LASTEXITCODE -ne 0) {
    & $python -m pip install 'setuptools>=77.0.3,<81' openai-whisper resemblyzer
    if ($LASTEXITCODE -ne 0) { throw 'Enhanced speech dependencies could not be installed.' }
}
& $python -c "import whisper; whisper.load_model('small.en', device='cpu'); from resemblyzer import VoiceEncoder; VoiceEncoder(device='cpu')"
if ($LASTEXITCODE -ne 0) { throw 'Enhanced speech models could not be loaded.' }
# Keep enhanced meeting dependencies, then use the same checked accuracy updater.
& "$PSScriptRoot\setup_accuracy.ps1"
