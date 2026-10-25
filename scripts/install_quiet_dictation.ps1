param([string]$QualityPython = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $env:LOCALAPPDATA 'Programs\LocalDictation'
$installed = Join-Path $destination 'Notewll.exe'
$build = Join-Path $root 'build\Notewll.exe'
if (-not (Test-Path -LiteralPath $installed) -and -not (Test-Path -LiteralPath (Join-Path $destination 'LocalDictation.exe'))) { throw 'Install the latest Notewll Setup first.' }
$payload = @(
    @{ Source = $build; Name = 'Notewll.exe' },
    @{ Source = (Join-Path $root 'engine\quality_worker.py'); Name = 'engine\quality_worker.py' },
    @{ Source = (Join-Path $root 'engine\accurate_worker.py'); Name = 'engine\accurate_worker.py' },
    @{ Source = (Join-Path $root 'assets\silero_vad.onnx'); Name = 'engine\silero_vad.onnx' },
    @{ Source = (Join-Path $root 'THIRD_PARTY_NOTICES.md'); Name = 'THIRD_PARTY_NOTICES.md' },
    # Preserve existing pinned shortcuts and startup entries while upgrading.
    @{ Source = $build; Name = 'LocalDictation.exe' }
)
foreach ($item in $payload) {
    if (-not (Test-Path -LiteralPath $item.Source)) { throw "Build Notewll first. Missing: $($item.Source)" }
}
if ((Get-FileHash -LiteralPath $payload[3].Source).Hash.ToLowerInvariant() -ne '1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3') { throw 'Speech detector integrity check failed.' }
# Check access before closing the app. A denied update must leave it running.
$probe = Join-Path $destination ('.notewll-write-check-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try { $stream = [IO.File]::Open($probe, 'CreateNew', 'Write', 'None'); $stream.Dispose() }
finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
$backup = Join-Path $root ('output\installed-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach ($item in $payload) {
    $target = Join-Path $destination $item.Name
    $item.Exists = Test-Path -LiteralPath $target
    if ($item.Exists) {
        $saved = Join-Path $backup $item.Name
        New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
    }
}
$configPath = Join-Path $env:LOCALAPPDATA 'LocalDictation\config.json'
if ($QualityPython) {
    if (-not (Test-Path -LiteralPath $QualityPython)) { throw 'The enhanced Python environment is missing.' }
    if (-not (Test-Path -LiteralPath $configPath)) { throw 'Open Notewll once before enabling accurate speech.' }
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $backup 'config.json')
    $next = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $next | Add-Member AccurateDictationEnabled $true -Force
    $next | Add-Member QualityPython $QualityPython -Force
    $preparedConfig = Join-Path $backup 'config-next.json'
    $next | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $preparedConfig -Encoding UTF8
    & $build --validate-config $preparedConfig | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Updated preferences did not validate; the running app was left unchanged.' }
}
Write-Host 'Updating Notewll. Finish any active dictation before running this update.'
$running = @(Get-Process Notewll,LocalDictation -ErrorAction SilentlyContinue | Where-Object { $_.Path -in @($installed, (Join-Path $destination 'LocalDictation.exe')) })
if ($running.Count) {
    & $build --request-exit | Out-Null
    foreach ($appProcess in $running) { if (-not $appProcess.WaitForExit(8000)) { throw 'Close Notewll from its tray menu and try again. No application files were changed.' } }
}
try {
    foreach ($item in $payload) {
        $target = Join-Path $destination $item.Name
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $item.Source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $item.Source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw "Installed file verification failed: $($item.Name)" }
    }
    if ($QualityPython) { Copy-Item -LiteralPath $preparedConfig -Destination $configPath -Force }
    Write-Host "Notewll updated. Backup: $backup"
} catch {
    foreach ($item in $payload) {
        $target = Join-Path $destination $item.Name
        if ($item.Exists) { Copy-Item -LiteralPath (Join-Path $backup $item.Name) -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    if ($QualityPython) { Copy-Item -LiteralPath (Join-Path $backup 'config.json') -Destination $configPath -Force }
    throw
} finally {
    Start-Process -FilePath $installed -ArgumentList '--background' -WorkingDirectory $destination -WindowStyle Hidden
}
