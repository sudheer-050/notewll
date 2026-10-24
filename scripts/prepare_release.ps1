param([string]$Repository = 'sudheer-050/notewll')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = '0.10.0'
$installer = Join-Path $root "dist\Notewll-Setup.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw 'Build the installer first.' }
$signature = Get-AuthenticodeSignature -LiteralPath $installer
$manifest = [ordered]@{
 Version = $version
 InstallerUrl = "https://github.com/$Repository/releases/download/v$version/Notewll-Setup.exe"
 Sha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'dist\update.json') -Encoding UTF8
$notes = [IO.File]::ReadAllText((Join-Path $root 'RELEASE_NOTES.md'))
$notes | Set-Content -LiteralPath (Join-Path $root 'dist\RELEASE_NOTES-0.10.0.md') -Encoding UTF8
[pscustomobject]@{Repository=$Repository;Installer=$installer;Signature=$signature.Status.ToString();UpdateManifest=(Join-Path $root 'dist\update.json');FeedUrl="https://github.com/$Repository/releases/latest/download/update.json";PublisherThumbprint=if ($signature.SignerCertificate) {$signature.SignerCertificate.Thumbprint} else {'Not configured'}} | ConvertTo-Json
Write-Host 'Prepared locally only. No GitHub repository, release or public page was changed.'
