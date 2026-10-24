$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\build_setup.ps1"
$setup = Join-Path $root 'dist\Notewll-Setup-0.10.0.exe'
# Setup must run interactively from the user's Windows session to install the small speech files.
Start-Process -FilePath $setup -WorkingDirectory $root
