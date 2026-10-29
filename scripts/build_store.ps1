param([string]$Version = '0.11.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$identity = Get-Content -LiteralPath "$root\store\identity.json" -Raw | ConvertFrom-Json
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Store version must have four components and end in .0.' }
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory | Where-Object { Test-Path "$($_.FullName)\x64\makeappx.exe" } | Sort-Object Name -Descending | Select-Object -First 1
if (!$sdk) { throw 'Install the Windows SDK with MSIX packaging tools.' }
$winmd = "${env:ProgramFiles(x86)}\Windows Kits\10\UnionMetadata\$($sdk.Name)\Windows.winmd"
$framework = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$output = Join-Path $root ("output\store-build-" + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $output 'package'
if (![IO.Path]::GetFullPath($stage).StartsWith([IO.Path]::GetFullPath($root) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Store staging must stay inside this checkout.' }
New-Item -ItemType Directory -Force "$stage\Assets", "$stage\native", "$stage\engine", "$root\dist" | Out-Null
$sources = Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName }
$versionSource = Join-Path $output 'StoreVersion.cs'
[IO.File]::WriteAllText($versionSource, "[assembly: System.Reflection.AssemblyVersion(""$Version"")]`r`n[assembly: System.Reflection.AssemblyFileVersion(""$Version"")]")
$sources += $versionSource
$references = @('System','System.Core','System.Drawing','System.Windows.Forms','System.Web.Extensions','System.Security','System.IO.Compression','System.IO.Compression.FileSystem','System.Runtime','System.Runtime.WindowsRuntime') | ForEach-Object { "/reference:$framework\$_.dll" }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /define:STORE_BUILD /win32icon:"$root\assets\notewll.ico" /resource:"$root\assets\notewll.ico",AppLogo.ico /resource:"$root\assets\notewll.png",AppLogo.png /win32manifest:"$root\src\app.manifest" /out:"$stage\Notewll.exe" /reference:"$winmd" $references $sources
if ($LASTEXITCODE) { throw 'Store application compilation failed.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo("$stage\Notewll.exe").FileVersion -ne $Version) { throw 'Compiled app version does not match the Store package version.' }
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:"$root\src\app.manifest" /out:"$stage\NotewllStartup.exe" "$root\store\Startup.cs"
if ($LASTEXITCODE) { throw 'Store startup launcher compilation failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$cache = Join-Path $env:LOCALAPPDATA 'LocalDictation\SetupCache'
$archives = @(
    @('vosk-model-small-en-us-0.15.zip','https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip','30f26242c4eb449f948e42cb302dd7a686cb29a3423a8367f99ff41780942498'),
    @('vosk-win64-0.3.45.zip','https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip','f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b')
)
New-Item -ItemType Directory -Force $cache | Out-Null
foreach ($archive in $archives) {
    $path = Join-Path $cache $archive[0]
    if (!(Test-Path -LiteralPath $path)) { Invoke-WebRequest -UseBasicParsing -Uri $archive[1] -OutFile $path }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $archive[2]) { throw "Invalid speech archive: $path" }
}
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $cache $archives[0][0]), "$output\model")
Move-Item -LiteralPath "$output\model\vosk-model-small-en-us-0.15" -Destination "$stage\model-small-en-us"
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $cache $archives[1][0]))
try { foreach ($entry in $zip.Entries) { if ($entry.Name -like '*.dll') { [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path "$stage\native" $entry.Name)) } } } finally { $zip.Dispose() }
Copy-Item "$root\engine\*.py" "$stage\engine"
Copy-Item "$root\scripts\setup_semantic.ps1", "$root\scripts\setup_handsfree.ps1", "$root\scripts\setup_optional_speech.ps1" "$stage\engine"
Copy-Item "$root\assets\silero_vad.onnx" "$stage\engine"
Copy-Item "$root\THIRD_PARTY_NOTICES.md", "$root\store\PRIVACY.md" $stage
Copy-Item "$root\store\licenses" "$stage\licenses" -Recurse
# Use the existing app artwork at native Store asset dimensions.
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Image]::FromFile("$root\assets\notewll.png")
try {
    $assets = @(@('StoreLogo',50,50), @('Square44x44Logo',44,44), @('Square150x150Logo',150,150), @('Square71x71Logo',71,71), @('Square310x310Logo',310,310), @('Wide310x150Logo',310,150), @('ListingIcon',1080,1080))
    foreach ($asset in $assets) {
        $bitmap = New-Object Drawing.Bitmap($asset[1],$asset[2]); $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent); $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $side = [int]([Math]::Min($asset[1],$asset[2]) * 0.9)
            $graphics.DrawImage($icon,[int](($asset[1]-$side)/2),[int](($asset[2]-$side)/2),$side,$side)
            $bitmap.Save("$stage\Assets\$($asset[0]).png",[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $icon.Dispose() }
[xml]$manifest = Get-Content -LiteralPath "$root\store\AppxManifest.xml"
$manifest.Package.Identity.SetAttribute('Version',$Version)
$manifest.Package.Identity.SetAttribute('Name',$identity.Name)
$manifest.Package.Identity.SetAttribute('Publisher',$identity.Publisher)
$manifest.Package.Properties.PublisherDisplayName = $identity.PublisherDisplayName
$manifest.Save("$stage\AppxManifest.xml")
# Verify the exact staged speech payload, without relying on the developer installation.
$check = Start-Process -FilePath "$stage\Notewll.exe" -ArgumentList '--installed-speech-check' -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$output\speech-check.txt" -RedirectStandardError "$output\speech-error.txt"
if ($check.ExitCode -ne 0 -or (Get-Content "$output\speech-check.txt" -Raw) -ne 'speech-ready|offline=true') { throw "Staged speech check failed. See $output" }
$package = Join-Path $root "dist\Notewll-Store-$Version-x64.msix"
& "$($sdk.FullName)\x64\makeappx.exe" pack /d $stage /p $package /o
if ($LASTEXITCODE) { throw 'MSIX validation/packaging failed.' }
$result = [ordered]@{Package=$package; SHA256=(Get-FileHash $package -Algorithm SHA256).Hash; Staging=$stage; Version=$Version; StoreId=$identity.StoreId; SpeechCheck='speech-ready|offline=true'; Signed=$false; InstalledTested=$false}
$result | ConvertTo-Json | Set-Content "$root\dist\Notewll-Store-build.json" -Encoding UTF8
$result | ConvertTo-Json
