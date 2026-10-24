param([string]$DataRoot = "$env:LOCALAPPDATA\LocalDictation")
$ErrorActionPreference = 'Stop'
$feature = Join-Path $DataRoot 'SemanticSearch'
New-Item -ItemType Directory -Force $feature | Out-Null
$wheelhouse = Join-Path $feature 'downloads'
New-Item -ItemType Directory -Force $wheelhouse | Out-Null
$python = Join-Path $feature 'runtime\Scripts\python.exe'
if (-not (Test-Path $python)) { & python -m venv (Join-Path $feature 'runtime'); if ($LASTEXITCODE) { throw 'Python 3.10 or later is required.' } }
# Download size is measured from actual downloaded packages, separately from installed bytes.
& $python -m pip download --dest $wheelhouse "sentence-transformers==3.4.1" "transformers==4.48.3"
if ($LASTEXITCODE) { throw 'Package download failed.' }
& $python -m pip install --no-index --find-links $wheelhouse "sentence-transformers==3.4.1" "transformers==4.48.3"
if ($LASTEXITCODE) { throw 'Package installation failed.' }
$env:NOTEWLL_SEMANTIC_MODEL = Join-Path $feature 'model'
& $python -c "import os; from sentence_transformers import SentenceTransformer; SentenceTransformer('sentence-transformers/all-MiniLM-L6-v2', device='cpu').save(os.environ['NOTEWLL_SEMANTIC_MODEL'])"
if ($LASTEXITCODE) { throw 'Model installation failed.' }
$packageBytes = (Get-ChildItem -LiteralPath $wheelhouse -Recurse -File | Measure-Object Length -Sum).Sum
$modelBytes = (Get-ChildItem -LiteralPath $env:NOTEWLL_SEMANTIC_MODEL -Recurse -File | Measure-Object Length -Sum).Sum
@{ package_download_bytes = $packageBytes; model_saved_bytes = $modelBytes; model_network_bytes = 'Not measured (hub cache may be reused)'; installed_utc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $feature 'installation.json')
Write-Host 'Semantic search installed. Searches run offline.'
