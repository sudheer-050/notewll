$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$built = Join-Path $root 'build\Notewll.exe'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\LocalDictation\Notewll.exe'
$log = Join-Path $root 'output\tray-update.log'
Start-Transcript -Path $log -Force | Out-Null
try {
    $source = Get-Item -LiteralPath $built
    $expected = (Get-FileHash -LiteralPath $built -Algorithm SHA256).Hash
    Write-Host "Applying Notewll build from $($source.LastWriteTime.ToString('yyyy-MM-dd h:mm:ss tt'))"
    Write-Host "Source SHA256: $expected"
    Write-Host 'Tray style: round recording and processing animation at the tray position.'
    Write-Host 'Finish any active recording before continuing.'
    & "$PSScriptRoot\install_quiet_dictation.ps1"
    $actual = (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw 'Installed app differs from the selected build.' }
    Write-Host 'SUCCESS: The installed application matches the latest build.' -ForegroundColor Green
    Write-Host "Installed SHA256: $actual"
} catch {
    Write-Host "UPDATE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'The latest tray changes have NOT been confirmed installed.'
    Write-Host "Details saved to: $log"
    Stop-Transcript | Out-Null
    exit 1
}
Stop-Transcript | Out-Null