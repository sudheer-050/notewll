$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = '0.13.1'
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $build, $dist | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw '.NET Framework 4.x compiler not found.' }
$sources = Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName }
# Default prompt guides ship inside the executable; users get editable copies in Documents\Notewll\Prompt guides.
$guides = Get-ChildItem "$root\prompt-guides\*.md" | ForEach-Object { "/resource:$($_.FullName),PromptGuide.$($_.Name)" }
# Google sign-in (desktop OAuth client id + installed-app secret) from an ignored local file; builds without it simply have sign-in unavailable.
if (Test-Path "$root\.local\google-oauth.txt") { $guides += "/resource:$root\.local\google-oauth.txt,NotewllCloud.GoogleClient" }
# UI Automation (reading the browser address bar) is part of the .NET Framework on every Windows PC; nothing extra ships.
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$root\assets\notewll.ico" /resource:"$root\assets\notewll.ico",AppLogo.ico /resource:"$root\assets\notewll.png",AppLogo.png $guides /win32manifest:"$root\src\app.manifest" /out:"$build\Notewll.exe" /reference:System.Security.dll /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:"$wpf\UIAutomationClient.dll" /reference:"$wpf\UIAutomationTypes.dll" /reference:"$wpf\WindowsBase.dll" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
# Legacy tooling may still open this filename; the current artifact is Notewll.exe.
Copy-Item "$build\Notewll.exe" "$build\LocalDictation.exe" -Force
Copy-Item "$build\Notewll.exe" "$dist\Notewll-$version.exe" -Force
Write-Host "Built $dist\Notewll-$version.exe"

New-Item -ItemType Directory -Force "$build\engine" | Out-Null
Copy-Item "$root\engine\quality_worker.py" "$build\engine\quality_worker.py" -Force
Copy-Item "$root\engine\accurate_worker.py" "$build\engine\accurate_worker.py" -Force
Copy-Item "$root\assets\silero_vad.onnx" "$build\engine\silero_vad.onnx" -Force

Copy-Item "$root\engine\voice_match_worker.py" "$build\engine\voice_match_worker.py" -Force
Copy-Item "$root\scripts\setup_handsfree.ps1" "$build\engine\setup_handsfree.ps1" -Force

Copy-Item "$root\engine\semantic_worker.py" "$build\engine\semantic_worker.py" -Force
Copy-Item "$root\scripts\setup_semantic.ps1" "$build\engine\setup_semantic.ps1" -Force
Copy-Item "$root\scripts\setup_optional_speech.ps1" "$build\engine\setup_optional_speech.ps1" -Force
