$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repository = 'sudheer-050/notewll'
$snapshot = Join-Path $root 'release\notewll-0.11.0'
function Run([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed. Source was not confirmed published." }
}
try {
    # Refresh from current source so publishing cannot restore a stale product name.
    & "$PSScriptRoot\build_setup.ps1"
    & "$PSScriptRoot\publish_release.ps1" -PrepareOnly
    if (!(Test-Path -LiteralPath (Join-Path $snapshot 'src\Program.cs'))) { throw 'Prepared source snapshot is missing.' }
    $files = Get-ChildItem -LiteralPath $snapshot -File -Recurse
    if ($files | Where-Object { $_.Extension -in '.exe','.wav','.pcm','.pfx','.p12' -or $_.FullName -match '[\\/]\.git[\\/]' }) { throw 'Unexpected binary, private data or Git metadata in snapshot.' }
    Run 'gh' @('api','user','--jq','.login')
    $checkout = Join-Path $root ('output\github-source-' + [Guid]::NewGuid().ToString('N'))
    Run 'gh' @('repo','clone',$repository,$checkout)
    $branch = (& git -C $checkout branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or !$branch) { throw 'Could not determine the default branch.' }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($snapshot.Length).TrimStart('\','/')
        $target = Join-Path $checkout $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Run 'git' @('-C',$checkout,'add','--all')
    & git -C $checkout diff --cached --quiet
    if ($LASTEXITCODE -eq 1) {
        $account = (& gh api user --jq '.login').Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not identify the GitHub account.' }
        Run 'git' @('-C',$checkout,'-c',"user.name=$account",'-c',"user.email=$account@users.noreply.github.com",'commit','-m','Update Notewll source, offline installer build and installation guide')
    } elseif ($LASTEXITCODE -ne 0) { throw 'Could not check staged changes.' }
    Run 'git' @('-C',$checkout,'push','origin',$branch)
    $local = (& git -C $checkout rev-parse HEAD).Trim()
    $remote = & git -C $checkout ls-remote origin "refs/heads/$branch"
    if ($LASTEXITCODE -ne 0 -or !($remote -match "^$local\s")) { throw 'Remote commit verification failed.' }
    Write-Host "Published and verified: https://github.com/$repository/commit/$local" -ForegroundColor Green
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host 'If GitHub login is required, run: gh auth login'
    exit 1
}
