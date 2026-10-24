param([string]$DataRoot = (Join-Path $env:LOCALAPPDATA 'LocalDictation'))
$ErrorActionPreference = 'Stop'
$environment = Join-Path $DataRoot 'VoiceMatching'
$python = Join-Path $environment 'Scripts\python.exe'
if (!(Test-Path -LiteralPath $python)) {
    if (Get-Command py -ErrorAction SilentlyContinue) { & py -3 -m venv --system-site-packages $environment }
    elseif (Get-Command python -ErrorAction SilentlyContinue) { & python -m venv --system-site-packages $environment }
    else { throw 'Install Python 3.10 or later for optional local voice matching, then run setup again.' }
    if ($LASTEXITCODE -ne 0) { throw 'Python could not create the local voice matching environment.' }
}
& $python -c 'import torch' 2>$null
if ($LASTEXITCODE -ne 0) {
    & $python -m pip install torch --index-url https://download.pytorch.org/whl/cpu
    if ($LASTEXITCODE -ne 0) { throw 'The CPU voice matching runtime could not be installed.' }
}
& $python -m pip install 'resemblyzer==0.1.4'
if ($LASTEXITCODE -ne 0) { throw 'Local voice matching dependencies could not be installed.' }
& $python -c "from resemblyzer import VoiceEncoder; VoiceEncoder(device='cpu', verbose=False)"
if ($LASTEXITCODE -ne 0) { throw 'The local voice model could not load.' }
Write-Output 'Local voice matching is ready. Open Notewll > Preferences > Hands-free to enroll.'
