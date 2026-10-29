# Run in Windows PowerShell as Administrator, using your usual Windows account.
# This signs a COPY; it does not modify or upload the Store submission artifact.
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Open Windows PowerShell as Administrator using your usual Windows account, then run this script again.' }
$root = Split-Path -Parent $PSScriptRoot
$identity = Get-Content "$root\store\identity.json" -Raw | ConvertFrom-Json
$build = Get-Content "$root\dist\Notewll-Store-build.json" -Raw | ConvertFrom-Json
if (!(Test-Path -LiteralPath $build.Package)) { throw 'Build the Store package first with scripts\build_store.ps1.' }
if ((Get-FileHash -LiteralPath $build.Package -Algorithm SHA256).Hash -ne $build.SHA256) { throw 'Store package hash differs from build metadata. Rebuild before signing.' }
if (Get-Process Notewll,LocalDictation -ErrorAction SilentlyContinue) { throw 'Exit Notewll from its tray menu, then run this script again.' }
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory | Where-Object { Test-Path "$($_.FullName)\x64\signtool.exe" } | Sort-Object Name -Descending | Select-Object -First 1
if (!$sdk) { throw 'Install the Windows SDK signing tools first.' }
$testFolder = Join-Path $root ('dist\store-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder | Out-Null
$testPackage = Join-Path $testFolder 'Notewll-Test.msix'
Copy-Item -LiteralPath $build.Package -Destination $testPackage
$certificate = New-SelfSignedCertificate -Type Custom -KeyUsage DigitalSignature -KeyExportPolicy NonExportable -CertStoreLocation 'Cert:\CurrentUser\My' -Subject $identity.Publisher -FriendlyName 'Notewll local test only' -NotAfter (Get-Date).AddDays(30) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
$thumbprint = $certificate.Thumbprint
# Save the certificate identifier before trust/install, including if a later step fails.
@{CertificateThumbprint=$thumbprint;TestPackage=$testPackage;OriginalPackage=$build.Package;OriginalSHA256=$build.SHA256} | ConvertTo-Json | Set-Content (Join-Path $testFolder 'test-install.json') -Encoding UTF8
& "$($sdk.FullName)\x64\signtool.exe" sign /fd SHA256 /sha1 $thumbprint /s My $testPackage
if ($LASTEXITCODE) { throw "Signing failed. Test files: $testFolder" }
$publicCertificate = Join-Path $testFolder 'Notewll-Test.cer'
Export-Certificate -Cert $certificate -FilePath $publicCertificate | Out-Null
# Trust only the public code-signing certificate, in Trusted People (not Root).
Import-Certificate -FilePath $publicCertificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
& "$($sdk.FullName)\x64\signtool.exe" verify /pa $testPackage
if ($LASTEXITCODE) { throw "Signature verification failed. Test files: $testFolder" }
Add-AppxPackage -Path $testPackage
Write-Host 'Installed. Open Notewll from Start; test it in a normal, non-administrator Notepad window.'
Write-Host "Test files: $testFolder"
Write-Host 'After testing, uninstall the test package from Windows Settings > Apps before installing the Store release.'
Write-Host 'Then remove only this test certificate using these commands in Administrator PowerShell:'
Write-Host "Remove-Item -LiteralPath 'Cert:\LocalMachine\TrustedPeople\$thumbprint'"
Write-Host "Remove-Item -LiteralPath 'Cert:\CurrentUser\My\$thumbprint'"
