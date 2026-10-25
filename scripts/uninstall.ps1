$ErrorActionPreference = 'Stop'
# Use the same safe uninstaller as Windows Installed apps; it preserves notes by default.
$uninstaller = Join-Path $env:LOCALAPPDATA 'Programs\LocalDictation\Uninstall.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Notewll is not installed, or its uninstaller is missing. Repair it with Notewll Setup first.' }
Start-Process -FilePath $uninstaller -ArgumentList '--uninstall'
