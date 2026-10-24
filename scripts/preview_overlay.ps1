# Exercise the real renderer without recording audio or sending keystrokes.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build\LocalDictation.exe'))
$overlayType = $assembly.GetType('LocalDictation.Overlay')
$overlay = [Activator]::CreateInstance($overlayType, $true)
$native = $assembly.GetType('LocalDictation.Native')
$foregroundBefore = $native.GetMethod('GetForegroundWindow').Invoke($null, @())
$output = Join-Path $root 'output\overlay-preview'
[void](New-Item -ItemType Directory -Path $output -Force)
function Wait-Animation([int]$frames) {
    for ($i = 0; $i -lt $frames; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 30 }
}
function Save-Frame([string]$name, [single]$scale = 1) {
    $bitmap = $overlayType.GetMethod('Snapshot').Invoke($overlay, @($scale))
    try {
        if ($bitmap.GetPixel(0, 0).A -ne 0) { throw 'Overlay corners must be transparent.' }
        if ($bitmap.Width -ne [Math]::Ceiling(64 * $scale)) { throw 'Overlay DPI scaling is incorrect.' }
        $bitmap.Save((Join-Path $output "$name.png"))
    } finally { $bitmap.Dispose() }
}
function Frame-Hash([string]$name) { return (Get-FileHash -LiteralPath (Join-Path $output "$name.png")).Hash }
try {
    $overlayType.GetField('InputLevel').SetValue($overlay, [Func[int]] { 0 })
    $overlayType.GetMethod('Display').Invoke($overlay, @('Recording from microphone', [Drawing.Color]::Gold))
    Wait-Animation 16; Save-Frame 'quiet'; Wait-Animation 16; Save-Frame 'quiet-next'
    if ((Frame-Hash 'quiet') -ne (Frame-Hash 'quiet-next')) { throw 'Indicator moved during silence.' }
    $overlayType.GetField('InputLevel').SetValue($overlay, [Func[int]] { 1600 })
    Wait-Animation 16; Save-Frame 'recording'; Wait-Animation 16; Save-Frame 'recording-next'
    if ((Frame-Hash 'recording') -eq (Frame-Hash 'recording-next')) { throw 'Speech did not animate.' }
    Save-Frame 'recording-150' 1.5; Save-Frame 'recording-200' 2
    $overlayType.GetField('InputLevel').SetValue($overlay, [Func[int]] { 0 })
    Wait-Animation 45; Save-Frame 'settled'; Wait-Animation 16; Save-Frame 'settled-next'
    if ((Frame-Hash 'settled') -ne (Frame-Hash 'settled-next')) { throw 'Indicator failed to settle after speech.' }
    $overlayType.GetMethod('Display').Invoke($overlay, @('Transcribing locally...', [Drawing.Color]::Gold))
    Save-Frame 'processing'
    if ($native.GetMethod('GetForegroundWindow').Invoke($null, @()) -ne $foregroundBefore) { throw 'Overlay stole input focus.' }
    Write-Output "PASS: still in silence, sound-reactive, settles afterward, transparent, 100/150/200% rendering, no focus change. Previews: $output"
} finally { $overlay.Dispose() }
