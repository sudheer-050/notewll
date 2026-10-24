$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build_setup.ps1"
$root = Split-Path -Parent $PSScriptRoot
# Use the same installer for every installation path, including its model setup and graceful shutdown.
Start-Process -FilePath (Join-Path $root 'dist\Notewll-Setup-0.10.0.exe') -WorkingDirectory $root
