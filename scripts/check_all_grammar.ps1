param([switch]$Offline, [switch]$SummarizeOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$comparison = Join-Path $root 'output\grammar-comparison'
New-Item -ItemType Directory -Force -Path $comparison | Out-Null
$hadFailure = $false
function Invoke-Evaluation([string[]]$EvaluationArguments, [string]$LogPath) {
    # Native stderr can contain progress or a traceback; record it and use the exit code.
    $ErrorActionPreference = 'Continue'
    & python @EvaluationArguments *> $LogPath
    return $LASTEXITCODE
}
if (-not $SummarizeOnly) {
foreach ($candidate in @('tiny', 'unbabel')) {
    foreach ($beam in @(1, 5)) {
        $mode = if ($beam -eq 1) { 'greedy' } else { 'beam5' }
        $folder = Join-Path $comparison "$candidate-$mode"
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        Write-Host "Testing $candidate ($mode), 120 sentences..."
        $arguments = @('scripts/check_tiny_grammar.py', '--candidate', $candidate, '--beams', "$beam", '--suite', 'verification/grammar_cases.json', '--output', $folder)
        if ($Offline) { $arguments += '--offline' }
        $code = Invoke-Evaluation $arguments (Join-Path $folder 'run.log')
        if ($code -ne 0) { $hadFailure = $true; Write-Host "Not completed. See $folder\run.log"; continue }
        if (-not $Offline) {
            Write-Host 'Repeating from cache with model downloads disabled...'
            $code = Invoke-Evaluation ($arguments + '--offline') (Join-Path $folder 'offline-run.log')
            if ($code -ne 0) { $hadFailure = $true; Write-Host 'Offline repeat failed.' }
        }
    }
}
}
$exe = Join-Path $root 'build\Notewll.exe'
$assembly = [Reflection.Assembly]::LoadFrom($exe)
$type = $assembly.GetType('Notewll.SmartCleanup')
$complete = $type.GetMethod('Complete'); $accept = $type.GetMethod('Accept')
$config = [Activator]::CreateInstance($assembly.GetType('Notewll.Config'), $true)
$cases = Get-Content 'verification/grammar_cases.json' -Raw | ConvertFrom-Json
$rules = foreach ($row in $cases) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $actual = $complete.Invoke($null, @($config, [string]$row.raw, [int]0))
    [pscustomobject]@{kind=$row.kind; raw=$row.raw; reference=$row.reference; actual=$actual; ms=$watch.Elapsed.TotalMilliseconds}
}
$rules | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $comparison 'builtin-results.json') -Encoding UTF8
Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Select-Object Hash,Path | ConvertTo-Json | Set-Content (Join-Path $comparison 'builtin-build.json') -Encoding UTF8
foreach ($folder in Get-ChildItem -LiteralPath $comparison -Directory) {
    $reportPath = Join-Path $folder.FullName 'result.json'
    if (-not (Test-Path -LiteralPath $reportPath)) { continue }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.status -ne 'completed') { continue }
    $guarded = foreach ($row in $report.cases) {
        $allowed = $accept.Invoke($null, @([string]$row.raw, [string]$row.actual))
        [pscustomobject]@{kind=$row.kind; raw=$row.raw; reference=$row.reference; candidate=$row.actual; actual=if($allowed){$row.actual}else{$row.raw}; accepted=$allowed; ms=$row.ms}
    }
    $guarded | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $folder.FullName 'guarded-results.json') -Encoding UTF8
}
& python 'scripts/summarize_grammar_tests.py'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Report: $comparison\SUMMARY.md"
if ($hadFailure) { Write-Host 'Some model tests did not complete. Missing runs are not passes.'; exit 1 }
