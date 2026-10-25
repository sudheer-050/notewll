$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\build.ps1"
$signCertificate = $env:NOTEWLL_SIGN_CERTIFICATE
if (-not $signCertificate) { $signCertificate = $env:LOCALDICTATION_SIGN_CERTIFICATE } # Existing build environments.
if ($signCertificate) { & "$PSScriptRoot\sign.ps1" -File "$root\build\Notewll.exe" -CertificateThumbprint $signCertificate; Copy-Item -LiteralPath "$root\build\Notewll.exe" -Destination "$root\dist\Notewll-0.10.0.exe" -Force; Copy-Item -LiteralPath "$root\build\Notewll.exe" -Destination "$root\build\LocalDictation.exe" -Force }
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$cache = "$env:LOCALAPPDATA\LocalDictation\SetupCache"
$packages = @(
    @('vosk-model-small-en-us-0.15.zip','https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip','30f26242c4eb449f948e42cb302dd7a686cb29a3423a8367f99ff41780942498'),
    @('vosk-win64-0.3.45.zip','https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip','f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b')
)
New-Item -ItemType Directory -Force -Path $cache | Out-Null
foreach ($package in $packages) {
    $archive = Join-Path $cache $package[0]
    if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest -UseBasicParsing -Uri $package[1] -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package[2]) { throw "Invalid speech archive: $archive" }
}
& $csc /nologo /target:winexe /platform:x64 /optimize+ /define:UNINSTALLER /win32icon:"$root\assets\notewll.ico" /win32manifest:"$root\src\app.manifest" /out:"$root\build\Notewll-Uninstall.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$root\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compilation failed.' }
if ($signCertificate) { & "$PSScriptRoot\sign.ps1" -File "$root\build\Notewll-Uninstall.exe" -CertificateThumbprint $signCertificate }
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"$root\assets\notewll.ico" /win32manifest:"$root\src\app.manifest" /out:"$root\dist\Notewll-Setup-0.10.0.exe" /resource:"$root\build\Notewll.exe",Application /resource:"$cache\vosk-model-small-en-us-0.15.zip",SpeechModelZip /resource:"$cache\vosk-win64-0.3.45.zip",SpeechRuntimeZip /resource:"$root\build\Notewll-Uninstall.exe",Uninstaller /resource:"$root\THIRD_PARTY_NOTICES.md",Notices /resource:"$root\engine\semantic_worker.py",SemanticWorker /resource:"$root\scripts\setup_semantic.ps1",SemanticSetup /resource:"$root\engine\voice_match_worker.py",VoiceMatchWorker /resource:"$root\scripts\setup_handsfree.ps1",VoiceMatchSetup /resource:"$root\engine\quality_worker.py",QualityWorker /resource:"$root\engine\accurate_worker.py",AccurateWorker /resource:"$root\assets\silero_vad.onnx",SpeechDetector /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$root\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
if ($signCertificate) { & "$PSScriptRoot\sign.ps1" -File "$root\dist\Notewll-Setup-0.10.0.exe" -CertificateThumbprint $signCertificate }
Write-Host "Built $root\dist\Notewll-Setup-0.10.0.exe"

Copy-Item -LiteralPath "$root\dist\Notewll-Setup-0.10.0.exe" -Destination "$root\dist\Notewll-Setup.exe" -Force
