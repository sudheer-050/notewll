param([string]$Output = 'output\speech-fixtures')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$fixtureRoot = (Resolve-Path -LiteralPath $Output).Path
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$samples = @(
    @{ id = 'basic'; text = 'Please send the report tomorrow. We should review the design before Friday.' },
    @{ id = 'names'; text = 'My name is Sudheer. I am Alex. We agreed to meet on Monday.' },
    @{ id = 'numbers'; text = 'The budget is forty two thousand dollars. The next meeting starts at nine thirty.' },
    @{ id = 'boundary'; text = 'We need to check the microphone and computer audio together so that everyone in the meeting can be heard clearly. The decision is to finish the update tomorrow.' }
)
$manifest = @()
try {
    $voices = @($speaker.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -like 'en-*' } | Select-Object -First 2)
    if ($voices.Count -eq 0) { throw 'No installed English Windows speech voice is available.' }
    foreach ($voice in $voices) {
        $speaker.SelectVoice($voice.VoiceInfo.Name)
        foreach ($sample in $samples) {
            $id = ($voice.VoiceInfo.Name -replace '[^a-zA-Z0-9]', '-') + '-' + $sample.id
            $file = Join-Path $fixtureRoot ($id + '.wav')
            $speaker.SetOutputToWaveFile($file, $format)
            $speaker.Speak($sample.text)
            $speaker.SetOutputToNull()
            $manifest += [ordered]@{ id = $id; file = [IO.Path]::GetFileName($file); reference = $sample.text; kind = $sample.id; synthetic = $true }
        }
    }
} finally { $speaker.Dispose() }
ConvertTo-Json -InputObject $manifest -Depth 3 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'corpus.json') -Encoding UTF8
Write-Output "Generated $($manifest.Count) synthetic English clips. This is not an accent or human-meeting benchmark."
