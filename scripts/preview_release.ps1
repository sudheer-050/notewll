$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$f=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$configType=$a.GetType('Notewll.Config')
$configType.GetMethod('DiagnosticDataRoot',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@([string](Join-Path $root 'output/release-gallery/data'))) | Out-Null
$a.GetType('Notewll.SpeechEngine').GetMethod('DiagnosticRoot',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@([string](Join-Path $root 'output/release-gallery/offline-app'))) | Out-Null
$config=[Activator]::CreateInstance($configType,$true)
$storeType=$a.GetType('Notewll.NoteStore')
$store=[Activator]::CreateInstance($storeType,[object[]]@([string](Join-Path $root 'output/release-gallery/notes')))
$hubType=$a.GetType('Notewll.SettingsForm')
$hub=[Activator]::CreateInstance($hubType,[object[]]@($config,'','',$store))
try {
 [Windows.Forms.Application]::EnableVisualStyles(); $hub.Show(); $hub.ShowPage('Home'); [Windows.Forms.Application]::DoEvents()
 $image=[Drawing.Bitmap]::new($hub.Width,$hub.Height)
 try { $hub.DrawToBitmap($image,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$hub.Size)); $image.Save((Join-Path $root 'assets/showcase/notewll-0.9.0-home.png')) } finally { $image.Dispose() }
 $catalog=$a.GetType('Notewll.BallCatalog')
 $texture=$catalog.GetMethod('Texture',[Reflection.BindingFlags]'Static,NonPublic',$null,[type[]]@([string]),$null)
 foreach($name in @('Moon','Earth','Basketball','Jupiter')) { $texture.Invoke($null,@($name)).Save((Join-Path $root ('assets/showcase/ball-'+$name.ToLowerInvariant()+'.png'))) }
} finally { $hub.Dispose() }
