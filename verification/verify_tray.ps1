$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$type = $assembly.GetType('Notewll.FloatingDock')
$private = [Reflection.BindingFlags]'Instance,NonPublic'
$dock = [Activator]::CreateInstance($type, $true)
function Frame {
    $image = New-Object Drawing.Bitmap($dock.Width, $dock.Height)
    $dock.DrawToBitmap($image, (New-Object Drawing.Rectangle([Drawing.Point]::Empty, $dock.Size)))
    return $image
}
try {
    $type.GetField('hover', $private).GetValue($dock).Stop()
    $dock.RestorePosition([int]::MinValue, [int]::MinValue)
    $dock.Show()
    $type.GetMethod('SetExpanded', $private).Invoke($dock, @($true, $true)) | Out-Null
    $before = Frame
    try {
        for ($cycle = 0; $cycle -lt 12; $cycle++) {
            $dock.UpdateState($true, $false, $false, $false, $false, 'Recording')
            $during = Frame
            $during.Dispose()
            $type.GetMethod('SetExpanded', $private).Invoke($dock, @($false, $true)) | Out-Null
            $type.GetMethod('SetExpanded', $private).Invoke($dock, @($true, $true)) | Out-Null
            $dock.UpdateState($false, $false, $false, $false, $false, 'Ready')
        }
        $after = Frame
        try {
            for ($y = 0; $y -lt $before.Height; $y++) {
                for ($x = 0; $x -lt $before.Width; $x++) {
                    if ($before.GetPixel($x,$y) -ne $after.GetPixel($x,$y)) { throw "Paint residue at $x,$y" }
                }
            }
        } finally { $after.Dispose() }
    } finally { $before.Dispose() }
    'tray-repaint-ok|12-recording-and-expand-cycles|no-bitmap-residue'
} finally { $dock.Dispose() }