$ErrorActionPreference = 'Stop'
$app = Join-Path $env:LOCALAPPDATA 'Programs\LocalDictation\Notewll.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Install Notewll first, then run setup_accuracy.ps1.' }
# Accurate shortcut dictation does not require the separate Ollama grammar service.
Start-Process -FilePath $app -WorkingDirectory (Split-Path -Parent $app)
