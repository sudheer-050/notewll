$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$a = [Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$t = $a.GetType('Notewll.FloatingDock')
$f = [Reflection.BindingFlags]'Instance,NonPublic'
$d = [Activator]::CreateInstance($t,$true)
function Assert($ok,$message) { if (!$ok) { throw $message } }
try {
    $t.GetField('hover',$f).GetValue($d).Stop()
    $d.RestorePosition([int]::MinValue,[int]::MinValue)
    $d.Show()
    $d.Configure($false)
    $set = $t.GetMethod('SetExpanded',$f)
    $step = $t.GetMethod('AdvanceMorph',$f)
    Assert ([Math]::Max($d.Width,$d.Height) -le 56 -and $d.Width -eq $d.Height) 'Idle tray is too large.'
    Assert ($d.Opacity -lt 1 -and $d.Opacity -gt .8) 'Tray transparency missing.'
    function Capture($name) {
        $bitmap = [Drawing.Bitmap]::new($d.Width,$d.Height)
        try {
            $d.DrawToBitmap($bitmap,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$d.Size))
            for ($y=0; $y -lt $d.Height; $y++) { for ($x=0; $x -lt $d.Width; $x++) {
                if (!$d.Region.IsVisible($x,$y)) { $bitmap.SetPixel($x,$y,[Drawing.Color]::Transparent) }
            } }
            $bitmap.Save((Join-Path $root ('output/half-morph-' + $name + '.png')))
        } finally { $bitmap.Dispose() }
    }
    $checkHover = $t.GetMethod('CheckHover',$f)
    $checkHover.Invoke($d,@()) | Out-Null
    Assert (!$t.GetField('expanded',$f).GetValue($d)) 'Hover opened the tray without a click.'
    $native = $a.GetType('Notewll.Native').GetMethod('SendMessage')
    $point = [IntPtr](($d.Height / 2 -shl 16) -bor ($d.Width / 2))
    $native.Invoke($null,@([IntPtr]$d.Handle,[uint32]0x201,[IntPtr]1,$point)) | Out-Null
    $native.Invoke($null,@([IntPtr]$d.Handle,[uint32]0x202,[IntPtr]::Zero,$point)) | Out-Null
    Assert ($t.GetField('expanded',$f).GetValue($d)) 'Click did not open the tray.'
    $set.Invoke($d,@($false,$true)) | Out-Null
    Capture 'dots'
    $idle = $d.Width
    $set.Invoke($d,@($true,$false)) | Out-Null
    $step.Invoke($d,@()) | Out-Null
    $intermediate = $d.Width
    1..12 | ForEach-Object { $step.Invoke($d,@()) | Out-Null }
    Capture 'pop'
    Assert ($t.GetField('morph',$f).GetValue($d) -gt 0) 'No intermediate morph frame.'
    1..45 | ForEach-Object { $step.Invoke($d,@()) | Out-Null }
    Capture 'icons'
    Assert ($d.Width -gt $intermediate -and [Math]::Max($d.Width,$d.Height) -le [Math]::Ceiling(114 * $t.GetField("dpi",$f).GetValue($d))) 'Expanded tray is too large or did not animate.'
    $set.Invoke($d,@($false,$false)) | Out-Null
    Assert ($t.GetField('morphAnimation',$f).GetValue($d).Enabled) 'Collapse does not begin immediately.'
    1..26 | ForEach-Object { $step.Invoke($d,@()) | Out-Null }
    Assert ($d.Width -eq $idle -and $t.GetField('morph',$f).GetValue($d) -eq 0) 'Reverse morph does not restore dots.'
    $d.Configure($true)
    $set.Invoke($d,@($true,$false)) | Out-Null
    Assert ($t.GetField('morph',$f).GetValue($d) -eq 1) 'Reduced motion did not skip animation.'
    $area = [Windows.Forms.Screen]::FromControl($d).WorkingArea
    foreach ($scale in @(1.0,1.75,2.0)) {
        $t.GetField('dpi',$f).SetValue($d,[single]$scale)
        foreach ($edge in @('top','bottom','left','right')) {
            $x = $area.Left + $area.Width / 2; $y = $area.Top + $area.Height / 2
            if ($edge -eq 'top') { $y = $area.Top }
            if ($edge -eq 'bottom') { $y = $area.Bottom - 1 }
            if ($edge -eq 'left') { $x = $area.Left }
            if ($edge -eq 'right') { $x = $area.Right - 1 }
            $set.Invoke($d,@($false,$true)) | Out-Null
            $d.RestorePosition($x,$y)
            $before = $d.Bounds
            Assert ($d.Width -eq $d.Height) 'Collapsed tray is not a full circle.'
            $set.Invoke($d,@($true,$true)) | Out-Null
            if ($edge -eq 'top' -or $edge -eq 'bottom') {
                Assert ([Math]::Abs(($before.Left + $before.Width / 2.0) - ($d.Left + $d.Width / 2.0)) -le 1) 'Top/bottom tray drifted sideways.'
            } else {
                Assert ([Math]::Abs(($before.Top + $before.Height / 2.0) - ($d.Top + $d.Height / 2.0)) -le 1) 'Side tray drifted vertically.'
            }
            Assert ([Math]::Abs([Math]::Max($d.Width,$d.Height) - 2 * [Math]::Min($d.Width,$d.Height)) -le 1) 'Tray is not a half circle.'
            $inside = 0
            for ($py=0; $py -lt $d.Height; $py+=2) { for ($px=0; $px -lt $d.Width; $px+=2) { if ($d.Region.IsVisible($px,$py)) { $inside++ } } }
            Assert ($inside -gt .65 * $d.Width * $d.Height / 4 -and $inside -lt .9 * $d.Width * $d.Height / 4) 'Region is not curved.'
            Assert ($area.Contains($d.Bounds)) 'Inward expansion left the screen.'
            $set.Invoke($d,@($false,$true)) | Out-Null
            Assert ($d.Bounds -eq $before) 'Collapse did not return to its anchor.'
        }
    }
    'tray-morph-ok|compact|translucent|intermediate-frame|reverse|no-collapse-delay|reduced-motion|centered-four-edges-three-scales|click-to-open'
} finally { $d.Dispose() }
