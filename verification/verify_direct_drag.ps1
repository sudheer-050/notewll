$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$root=Split-Path -Parent $PSScriptRoot
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$f=[Reflection.BindingFlags]'Instance,NonPublic'
$t=$a.GetType('Notewll.FloatingDock'); $d=[Activator]::CreateInstance($t,$true)
try {
  $t.GetField('hover',$f).GetValue($d).Stop(); $d.RestorePosition(400,0); $d.Show()
  $t.GetField('dragStart',$f).SetValue($d,[Windows.Forms.Cursor]::Position)
  $t.GetField('locationStart',$f).SetValue($d,[Drawing.Point]::new(400,100))
  $move=$t.GetMethod('AdvanceDrag',$f); $move.Invoke($d,@()) | Out-Null
  $expected=$d.Location
  1..10 | ForEach-Object { $move.Invoke($d,@()) | Out-Null; if ($d.Location -ne $expected) { throw 'Ball drifts while held still.' } }
  'direct-drag-ok|no-drift-while-held'
} finally { $d.Dispose() }
