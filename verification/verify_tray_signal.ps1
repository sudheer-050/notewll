$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$type = $assembly.GetType('Notewll.FloatingDock')
$private = [Reflection.BindingFlags]'Instance,NonPublic'
$dock = [Activator]::CreateInstance($type,$true)
$advance = $type.GetMethod('AdvanceSignal',$private)
$timer = $type.GetField('signalAnimation',$private).GetValue($dock)
function Assert($condition,$message) { if (!$condition) { throw $message } }
function Frame([string]$name='') {
    $bitmap = [Drawing.Bitmap]::new($dock.Width,$dock.Height)
    $stream = [IO.MemoryStream]::new()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $dock.DrawToBitmap($bitmap,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$dock.Size))
        if ($name) { $bitmap.Save((Join-Path $root "output/tray-signal-$name.png")) }
        $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        return [BitConverter]::ToString($sha.ComputeHash($stream.ToArray()))
    } finally { $bitmap.Dispose(); $stream.Dispose(); $sha.Dispose() }
}
function Advance([int]$ticks) { for($i=0;$i -lt $ticks;$i++) { $advance.Invoke($dock,@()) | Out-Null } }
function State($writing,$processing,$notes=$false,$meeting=$false) {
    $dock.UpdateState($writing,$processing,$notes,$notes,$meeting,'Test')
    $timer.Stop() # Advance real frames deterministically, with no microphone or wall-clock dependency.
}
try {
    $type.GetField('hover',$private).GetValue($dock).Stop()
    $dock.RestorePosition([int]::MinValue,[int]::MinValue)
    $dock.Show()
    $restPosition = $dock.Location; $restSize = $dock.Size
    $script:sample = 0
    $dock.InputLevel = [Func[int]] { $script:sample }
    State $true $false
    Assert $dock.HasVoiceIndicator 'Tray did not become the voice indicator.'
    Assert ($dock.Width -eq $dock.Height) 'Recording indicator must be circular.'
    Assert (@($dock.Controls | Where-Object { $_.GetType().Name -eq 'DockButton' -and $_.Visible }).Count -eq 0) 'Buttons overlap the voice indicator.'
    $quiet = Frame 'quiet'
    Advance 15
    Assert ((Frame) -eq $quiet) 'Signal moves during silence.'
    $script:sample = 1600
    Advance 12
    $speech = Frame 'recording'
    Advance 12
    Assert ((Frame) -ne $speech) 'Speech does not animate.'
    $script:sample = 0
    Advance 80
    $settled = Frame
    Advance 15
    Assert ((Frame) -eq $settled) 'Signal does not settle after speech.'
    State $false $true
    Assert ($type.GetField('signalMode',$private).GetValue($dock) -eq 2) 'Processing state did not replace recording.'
    $processing = Frame 'processing'
    Advance 12
    Assert ((Frame) -eq $processing) 'Processing pretends to be microphone activity.'
    State $false $false
    Assert (!$dock.HasVoiceIndicator -and $dock.Location -eq $restPosition -and $dock.Size -eq $restSize) 'Normal tray did not return to its saved position.'
    $type.GetMethod('SetExpanded',$private).Invoke($dock,@($true,$true)) | Out-Null
    Assert (@($dock.Controls | Where-Object { $_.GetType().Name -eq 'DockButton' -and $_.Visible }).Count -eq 3) 'Normal icons were not restored.'
    $dock.Configure($true)
    State $true $false
    $still = Frame
    $script:sample = 1600
    Advance 12
    Assert ((Frame) -eq $still -and !$timer.Enabled) 'Reduced-motion preference was ignored.'
    $dock.SetVisibility($false)
    Assert (!$dock.HasVoiceIndicator -and !$timer.Enabled) 'Hidden tray still owns or animates the voice indicator.'
    $dock.SetVisibility($true)
    State $false $false $true $true
    Assert $dock.HasVoiceIndicator 'Meeting recording did not use the tray.'
    $area = [Windows.Forms.Screen]::FromControl($dock).WorkingArea
    foreach($scale in @(1.0,1.75,2.0)) {
        $type.GetField('dpi',$private).SetValue($dock,[single]$scale)
        foreach($point in @([Drawing.Point]::new($area.Left,$area.Top+$area.Height/2),[Drawing.Point]::new($area.Right-1,$area.Top+$area.Height/2),[Drawing.Point]::new($area.Left+$area.Width/2,$area.Top),[Drawing.Point]::new($area.Left+$area.Width/2,$area.Bottom-1))) {
            $dock.RestorePosition($point.X,$point.Y)
            Assert ($area.Contains($dock.Bounds)) 'Voice tray leaves the screen.'
            Assert ($dock.Width -eq $dock.Height) 'Indicator becomes elongated at this edge or DPI.'
            Assert (@($dock.Controls | Where-Object { $_.GetType().Name -eq 'DockButton' -and $_.Visible }).Count -eq 0) 'Hover/relayout brings icons back over the signal.'
            if($scale -eq 2.0 -and $point.Y -eq $area.Bottom-1) { Frame 'recording-200' | Out-Null }
        }
    }
    State $false $false
    $script:stopClicks = 0; $script:cancelClicks = 0; $script:noteClicks = 0; $script:meetingClicks = 0
    $dock.ToggleWrite = [Action] { $script:stopClicks++ }
    $dock.CancelWrite = [Action] { $script:cancelClicks++ }
    $dock.ToggleNote = [Action] { $script:noteClicks++ }
    $dock.ToggleMeeting = [Action] { $script:meetingClicks++ }
    $send = $assembly.GetType('Notewll.Native').GetMethod('SendMessage')
    foreach ($kind in @('dictation','processing','note','meeting')) {
        State ($kind -eq 'dictation') ($kind -eq 'processing') ($kind -eq 'note' -or $kind -eq 'meeting') ($kind -eq 'meeting')
        $point = [IntPtr](($dock.Height / 2 -shl 16) -bor ($dock.Width / 2))
        $send.Invoke($null,@([IntPtr]$dock.Handle,[uint32]0x201,[IntPtr]1,$point)) | Out-Null
        $send.Invoke($null,@([IntPtr]$dock.Handle,[uint32]0x202,[IntPtr]::Zero,$point)) | Out-Null
    }
    Assert ($script:stopClicks -eq 1 -and $script:cancelClicks -eq 1 -and $script:noteClicks -eq 1 -and $script:meetingClicks -eq 1) 'Clicking the signal does not stop/cancel the correct activity.'
    $dock.ShowVoiceIndicator = $false
    State $true $false
    Assert (!$dock.HasVoiceIndicator) 'Disabled voice indicator preference was ignored.'
    $dock.ShowVoiceIndicator = $true
    State $false $false
    'tray-signal-ok|quiet-and-speech|processing|restores-tray|stop-and-cancel-clicks|hidden-fallback|reduced-motion|four-edges|100-175-200-dpi'
} finally { $dock.Dispose() }