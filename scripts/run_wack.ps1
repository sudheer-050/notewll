#Requires -RunAsAdministrator
# Runs the Windows App Certification Kit against the newest Notewll Store MSIX.
# Run from an Administrator PowerShell:  powershell -ExecutionPolicy Bypass -File scripts\run_wack.ps1
$root = Split-Path -Parent $PSScriptRoot
$pkg = Get-ChildItem "$root\dist\Notewll-Store-*-x64.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$pkg) { throw 'No Store MSIX in dist. Run scripts\build_store.ps1 first.' }
$kit = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe"
$out = Join-Path $root ("verification\wack-" + ($pkg.BaseName -replace '^Notewll-Store-|-x64$',''))
New-Item -ItemType Directory -Force $out | Out-Null
& $kit reset
& $kit test -appxpackagepath $pkg.FullName -reportoutputpath "$out\wack-report.xml"
"WACK exit code: $LASTEXITCODE  (0 = passed). Report: $out\wack-report.xml"
