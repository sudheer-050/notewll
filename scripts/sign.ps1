param([Parameter(Mandatory=$true)][string]$File, [Parameter(Mandatory=$true)][string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
$targetFile = (Resolve-Path -LiteralPath $File).Path
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey) { throw 'The selected certificate has no private key available for signing.' }
$signature = Set-AuthenticodeSignature -LiteralPath $targetFile -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
if ($signature.Status -ne 'Valid') { throw "Signing failed: $($signature.StatusMessage)" }
Write-Host "Signed $targetFile"
