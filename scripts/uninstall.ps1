$ErrorActionPreference = 'Stop'
Get-Process LocalDictation -ErrorAction SilentlyContinue | Stop-Process
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name LocalDictation -ErrorAction SilentlyContinue
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Local Dictation.lnk'
if (Test-Path $shortcut) { Remove-Item -LiteralPath $shortcut }
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\LocalDictation'
if (Test-Path $installDir) { Remove-Item -LiteralPath $installDir -Recurse }
Write-Host 'Local Dictation was uninstalled. Preferences remain under LocalAppData\LocalDictation.'
