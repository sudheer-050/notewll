$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\build\Notewll.exe'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$appType = $assembly.GetType('Notewll.DictationApp')
function New-Internal($name) { [Activator]::CreateInstance($assembly.GetType("Notewll.$name"), $true) }
function Set-Field($app, $name, $value) { $appType.GetField($name, $flags).SetValue($app, $value) }

# Exercise the actual empty-result handler without installing keyboard hooks or opening the hub.
foreach ($inputText in @('', '   ')) {
    $app = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($appType)
    $state = New-Internal 'StateMachine'
    $stateType = $state.GetType()
    $enum = $assembly.GetType('Notewll.AppState')
    foreach ($step in @('Recording', 'Processing')) {
        $stateType.GetMethod('Move').Invoke($state, @([Enum]::Parse($enum, $step))) | Out-Null
    }
    $overlay = New-Internal 'Overlay'
    $timer = New-Object System.Windows.Forms.Timer
    $recovery = New-Internal 'DictationRecovery'
    $recovery.GetType().GetField('Audio').SetValue($recovery, [byte[]]@(1, 2))
    Set-Field $app 'state' $state
    Set-Field $app 'config' (New-Internal 'Config')
    Set-Field $app 'overlay' $overlay
    Set-Field $app 'liveTimer' $timer
    Set-Field $app 'recovery' $recovery
    Set-Field $app 'liveInserted' ''
    try {
        $appType.GetMethod('Finish', $flags).Invoke($app, @($inputText, $inputText)) | Out-Null
        if ($state.Current.ToString() -ne 'Idle' -or $overlay.Visible -or $recovery.HasWork) {
            throw 'Empty recognition did not finish quietly.'
        }
        # A short/silent recording has already disposed its recorder before cancellation.
        $stateType.GetMethod('Move').Invoke($state, @([Enum]::Parse($enum, 'Recording'))) | Out-Null
        $appType.GetMethod('Cancel', $flags).Invoke($app, @()) | Out-Null
        if ($state.Current.ToString() -ne 'Idle') { throw 'Silent recording did not return to idle.' }
    } finally { $overlay.Dispose(); $timer.Dispose() }
}
'quiet-dictation-ok: empty and whitespace results, released recorder, idle, no popup, no recovery'
