$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$f=[Reflection.BindingFlags]'Instance,NonPublic'
$t=$a.GetType('Notewll.BallPhysics')
$step=$t.GetMethod('Step',$f); $reset=$t.GetMethod('Reset',$f)
function Assert($ok,$why) { if (!$ok) { throw $why } }
foreach($scale in @(1.0,1.75,2.0)) {
  foreach($area in @([Drawing.Rectangle]::new(0,0,1280,720),[Drawing.Rectangle]::new(-1280,-200,1280,900))) {
    $b=[Activator]::CreateInstance($t,$true); $size=[Drawing.Size]::new([int](28*$scale),[int](28*$scale))
    $reset.Invoke($b,@([Drawing.PointF]::new($area.Left+200,$area.Top+60),[Drawing.PointF]::new(650*$scale,-250*$scale))) | Out-Null
    $bounced=$false
    for($i=0;$i -lt 1600;$i++) {
      $vBefore=$t.GetField('Velocity',$f).GetValue($b)
      $step.Invoke($b,@($area,$size,[single](1/120),[single]$scale)) | Out-Null
      $p=$t.GetField('Position',$f).GetValue($b); $v=$t.GetField('Velocity',$f).GetValue($b)
      Assert ($p.X -ge $area.Left-.01 -and $p.X -le $area.Right-$size.Width+.01 -and $p.Y -ge $area.Top-.01 -and $p.Y -le $area.Bottom-$size.Height+.01) 'Ball left the working area.'
      if($vBefore.Y -gt 0 -and $v.Y -lt 0) { $bounced=$true }
      if($t.GetField('Settled',$f).GetValue($b)) { break }
    }
    Assert $bounced 'Ball never bounced.'
    Assert ($t.GetField('Settled',$f).GetValue($b)) 'Ball did not settle.'
    Assert ([Math]::Abs($p.Y-($area.Bottom-$size.Height)) -lt .01 -and $v.IsEmpty) 'Ball did not stop on the bottom edge.'
  }
}
$dt=$a.GetType('Notewll.FloatingDock'); $d=[Activator]::CreateInstance($dt,$true)
try {
  $dt.GetField('hover',$f).GetValue($d).Stop(); $d.RestorePosition(400,0); $d.Show(); $d.Configure($false)
  $d.Location=[Drawing.Point]::new(400,100)
  $dt.GetMethod('BeginFall',$f).Invoke($d,@()) | Out-Null
  Assert ($dt.GetField('falling',$f).GetValue($d).Enabled) 'Release does not start falling.'
  $dt.GetField('falling',$f).GetValue($d).Stop()
  $d.Location=[Drawing.Point]::new(400,100)
  $native=$a.GetType('Notewll.Native').GetMethod('SendMessage')
  $point=[IntPtr](($d.Height/2 -shl 16) -bor ($d.Width/2))
  $native.Invoke($null,@([IntPtr]$d.Handle,[uint32]0x201,[IntPtr]1,$point)) | Out-Null
  $dt.GetField('dragMoved',$f).SetValue($d,$true)
  $native.Invoke($null,@([IntPtr]$d.Handle,[uint32]0x202,[IntPtr]::Zero,$point)) | Out-Null
  Assert ($dt.GetField('falling',$f).GetValue($d).Enabled) 'Native mouse release did not start the fall.'
  $watch=[Diagnostics.Stopwatch]::StartNew()
  while($dt.GetField('falling',$f).GetValue($d).Enabled -and $watch.Elapsed.TotalSeconds -lt 8) {
    [Windows.Forms.Application]::DoEvents(); [Threading.Thread]::Sleep(8)
  }
  Assert (!$dt.GetField('falling',$f).GetValue($d).Enabled) 'Live UI timer did not settle the ball.'
  Assert ($d.Bottom -eq [Windows.Forms.Screen]::FromControl($d).WorkingArea.Bottom) 'Live ball did not land at the bottom.'
  $d.Configure($true)
  Assert (!$dt.GetField('falling',$f).GetValue($d).Enabled) 'Reduced motion does not stop physics.'
  Assert ($d.Bottom -eq [Windows.Forms.Screen]::FromControl($d).WorkingArea.Bottom) 'Reduced-motion tray did not settle at the bottom.'
  Assert ($a.GetType('Notewll.CometTrail') -eq $null) 'Fire/trail code is still in the app.'
  'ball-physics-ok|gravity|momentum|bounces|screen-collisions|friction|settles-at-bottom|three-dpi-scales|negative-monitor-coordinates|reduced-motion|no-fire-or-trails|native-release|live-timer-settles'
} finally { $d.Dispose() }
