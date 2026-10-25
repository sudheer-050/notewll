$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build/Notewll.exe'))
$private = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $assembly.GetType('Notewll.FloatingDock')
$buttonType = $assembly.GetType('Notewll.DockButton')
$dock = [Activator]::CreateInstance($type, $true)
function Paint-Button($button, $graphics, $offset) {
    $state = $graphics.Save()
    try {
        $graphics.TranslateTransform($offset.X,$offset.Y)
        $graphics.SetClip($button.ClientRectangle)
        if ($button.Region) { $graphics.SetClip($button.Region,[Drawing.Drawing2D.CombineMode]::Intersect) }
        $paintArgs = [Windows.Forms.PaintEventArgs]::new($graphics,$button.ClientRectangle)
        try {
            $buttonType.GetMethod('OnPaintBackground',$private).Invoke($button,[object[]]@($paintArgs.PSObject.BaseObject))
            $buttonType.GetMethod('OnPaint',$private).Invoke($button,[object[]]@($paintArgs.PSObject.BaseObject))
        } finally { $paintArgs.Dispose() }
    } finally { $graphics.Restore($state) }
}
try {
    $type.GetField('hover',$private).GetValue($dock).Stop()
    $dock.RestorePosition([int]::MinValue,[int]::MinValue)
    $type.GetMethod('SetExpanded',$private).Invoke($dock,@($true,$true)) | Out-Null
    $buttons = @($dock.Controls | Where-Object { $_.GetType() -eq $buttonType } | Sort-Object Left,Top)
    $surface = New-Object Drawing.Bitmap($dock.Width,$dock.Height)
    $graphics = [Drawing.Graphics]::FromImage($surface)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(29,29,32))
        foreach ($button in $buttons) { Paint-Button $button $graphics $button.Location }
        # Each translated icon must match rendering that icon at its local origin.
        foreach ($button in $buttons) {
            $local = New-Object Drawing.Bitmap($button.Width,$button.Height)
            $localGraphics = [Drawing.Graphics]::FromImage($local)
            try {
                Paint-Button $button $localGraphics ([Drawing.Point]::Empty)
                for ($y=0; $y -lt $button.Height; $y++) {
                    for ($x=0; $x -lt $button.Width; $x++) {
                        if ((!$button.Region -or $button.Region.IsVisible($x,$y)) -and $surface.GetPixel($button.Left+$x,$button.Top+$y) -ne $local.GetPixel($x,$y)) {
                            throw "Translated/partial repaint corrupts $($button.Symbol) at $x,$y."
                        }
                    }
                }
            } finally { $localGraphics.Dispose(); $local.Dispose() }
        }
        # Hover lighting is disabled: every pixel must remain unchanged.
        foreach ($button in $buttons) {
            $before = $surface.Clone()
            try {
                $buttonType.GetMethod('OnMouseEnter',$private).Invoke($button,@([EventArgs]::Empty)) | Out-Null
                Paint-Button $button $graphics $button.Location
                for ($y=0; $y -lt $surface.Height; $y++) {
                    for ($x=0; $x -lt $surface.Width; $x++) {
                        if ($surface.GetPixel($x,$y) -ne $before.GetPixel($x,$y)) {
                            throw "Hovering $($button.Symbol) changes a pixel at $x,$y."
                        }
                    }
                }
                $buttonType.GetMethod('OnMouseLeave',$private).Invoke($button,@([EventArgs]::Empty)) | Out-Null
                Paint-Button $button $graphics $button.Location
            } finally { $before.Dispose() }
        }
        # Exercise actual window hover messages and the normal WinForms paint pipeline too.
        $savedCursor = [Windows.Forms.Cursor]::Position
        [Windows.Forms.Cursor]::Position = [Drawing.Point]::new($dock.Left + $dock.Width / 2, $dock.Top + $dock.Height / 2)
        $dock.Show()
        [Windows.Forms.Application]::DoEvents()
        $native = $assembly.GetType('Notewll.Native').GetMethod('SendMessage')
        $nativeBefore = [Drawing.Bitmap]::new($dock.Width,$dock.Height)
        $dock.DrawToBitmap($nativeBefore,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$dock.Size))
        try {
            foreach ($button in $buttons) {
                $point = [IntPtr]([int](([int]($button.Height / 2) -shl 16) -bor [int]($button.Width / 2)))
                foreach ($message in @(0x200,0x2A1,0x2A3)) {
                    $native.Invoke($null,@([IntPtr]$button.Handle,[uint32]$message,[IntPtr]::Zero,$point)) | Out-Null
                    $type.GetMethod("SetExpanded",$private).Invoke($dock,@($true,$true)) | Out-Null
                    [Windows.Forms.Application]::DoEvents()
                    $nativeAfter = [Drawing.Bitmap]::new($dock.Width,$dock.Height)
                    try {
                        $dock.DrawToBitmap($nativeAfter,[Drawing.Rectangle]::new([Drawing.Point]::Empty,$dock.Size))
                        for ($y=0; $y -lt $nativeBefore.Height; $y++) {
                            for ($x=0; $x -lt $nativeBefore.Width; $x++) {
                                if ($nativeBefore.GetPixel($x,$y) -ne $nativeAfter.GetPixel($x,$y)) {
                                    throw "Native hover message $message changed a pixel at $x,$y."
                                }
                            }
                        }
                    } finally { $nativeAfter.Dispose() }
                }
            }
        } finally { $nativeBefore.Dispose() }
        'tray-hover-ok|translated-paint|native-hover-messages|three-buttons|all-pixels-unchanged'
    } finally { $graphics.Dispose(); $surface.Dispose() }
} finally { if ($savedCursor) { [Windows.Forms.Cursor]::Position = $savedCursor }; $dock.Dispose() }