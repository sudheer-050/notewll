param([string]$Repository = 'sudheer-050/notewll', [switch]$PrepareOnly, [string]$AccuracyReferences = '', [string]$AccuracyHypotheses = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = '0.11.0'
$stage = Join-Path $projectRoot "release\notewll-$version"
$installer = Join-Path $projectRoot "dist\Notewll-Setup.exe"
$releaseNotes = Join-Path $projectRoot "dist\RELEASE_NOTES-$version.md"
if ($Repository -notmatch '^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$') { throw 'Use owner/repository for the GitHub repository.' }
if (-not $PrepareOnly) {
    if (-not $AccuracyReferences -or -not $AccuracyHypotheses) { throw 'Verified accuracy references and hypotheses are required before publication. Use -PrepareOnly to build a local review package.' }
    & python "$PSScriptRoot\score_accuracy.py" $AccuracyReferences $AccuracyHypotheses
    if ($LASTEXITCODE -ne 0) { throw 'The reviewed accuracy gate did not pass. No source or release was published.' }
}
if (-not (Test-Path -LiteralPath $installer)) { throw 'Build and verify the installer first.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
# Explicit source allowlist excludes recordings, local configuration, caches and generated test data.
foreach ($folder in @('src','assets','installer','scripts','engine','verification','.github')) {
    $target = Join-Path $stage $folder
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot $folder) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/]__pycache__[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring((Join-Path $projectRoot $folder).Length).TrimStart('\','/')
        $destination = Join-Path $target $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
}
foreach ($file in @('.gitignore','README.md','USER_GUIDE.md','BACKLOG_STATUS.md','FIX_REPORT_0.7.0.md','UX_USER_REVIEW_0.6.1.md','RELEASE_NOTES.md','THIRD_PARTY_NOTICES.md','requirements-dev.txt','SPEECH_QUALITY_UPDATE.md','Install quiet dictation.cmd','Update Notewll accuracy.cmd','setup.ps1','Update Notewll.vbs')) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination (Join-Path $stage $file) -Force }
$tests = Join-Path $stage 'tests'; New-Item -ItemType Directory -Path $tests -Force | Out-Null
foreach ($file in @('test_app.py','test_setup.py','test_note_exports.py','test_release.py','test_completion.py','test_live_grammar.py','test_speech_quality.py','test_ux_fixes.py','test_accurate_speech.py','test_accuracy_scoring.py')) { Copy-Item -LiteralPath (Join-Path $projectRoot "tests\$file") -Destination (Join-Path $tests $file) -Force }
& "$PSScriptRoot\prepare_release.ps1" -Repository $Repository
if ($PrepareOnly) { Write-Output "Source snapshot prepared: $stage"; return }
function Invoke-Native([scriptblock]$Command) {
    # Run a native command without PowerShell turning stderr into a terminating error; returns output and exit code.
    $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $out = & $Command 2>&1 | ForEach-Object { "$_" }; $code = $LASTEXITCODE } finally { $ErrorActionPreference = $previous }
    [pscustomobject]@{ Output = ($out -join "`n"); Code = $code }
}
# Native stderr (e.g. git line-ending warnings) must not abort; every consequential command below checks its exit code.
$ErrorActionPreference = 'Continue'
$who = Invoke-Native { gh api user --jq '.login' }
if ($who.Code -ne 0) { throw "Could not reach GitHub with the current credentials: $($who.Output)" }
$account = $who.Output.Trim()
if ($account -ne $Repository.Split('/')[0]) { throw "GitHub is signed into $account; use -Repository $account/notewll or sign into the requested owner." }
$repoState = Invoke-Native { gh repo view $Repository --json name,isEmpty }
$exists = $repoState.Code -eq 0
if (-not $exists -and $repoState.Output -notmatch 'Could not resolve to a Repository|HTTP 404|Not Found') { throw "Could not check the repository: $($repoState.Output)" }
if (-not (Test-Path -LiteralPath (Join-Path $stage '.git'))) {
    & git -C $stage init --initial-branch=main
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the source snapshot.' }
}
& git -C $stage config user.name $account
& git -C $stage config user.email "$account@users.noreply.github.com"
& git -C $stage add --all
if ($LASTEXITCODE -ne 0) { throw 'Could not stage the release snapshot.' }
& git -C $stage diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    & git -C $stage commit -m "Release Notewll $version Windows preview"
    if ($LASTEXITCODE -ne 0) { throw 'Could not commit the release snapshot.' }
}
if (-not $exists) {
    & gh repo create $Repository --public --description 'Private English voice typing and meeting notes for Windows. Native offline speech, four dots, no Docker.'
    if ($LASTEXITCODE -ne 0) { throw 'Repository creation did not complete. Inspect GitHub before retrying.' }
} else {
    # Only continue into an existing repository when it is this release's own: empty, or its main history is the snapshot's.
    $remoteHead = Invoke-Native { git ls-remote "https://github.com/$Repository.git" refs/heads/main }
    $localHead = (& git -C $stage rev-parse HEAD).Trim()
    if ($remoteHead.Output.Trim() -and -not $remoteHead.Output.StartsWith($localHead)) { throw 'The repository already has different content; this script never overwrites it.' }
}
$remotes = & git -C $stage remote
if ($remotes -notcontains 'origin') { & git -C $stage remote add origin "https://github.com/$Repository.git" }
& git -C $stage push origin main
if ($LASTEXITCODE -ne 0) { throw 'Source push did not complete.' }
& git -C $stage rev-parse -q --verify "refs/tags/v$version" | Out-Null
if ($LASTEXITCODE -ne 0) { & git -C $stage tag "v$version" }
& git -C $stage push origin "v$version"
if ($LASTEXITCODE -ne 0) { throw 'Source was published, but the release tag was not pushed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $installer
$hashFile = Join-Path $projectRoot 'dist\SHA256SUMS.txt'
((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($installer)) | Set-Content -LiteralPath $hashFile -Encoding ASCII
$assets = @($installer, $hashFile)
if ($signature.Status -eq 'Valid') { $assets += (Join-Path $projectRoot 'dist\update.json') }
$title = "Notewll $version - Windows preview" + $(if ($signature.Status -eq 'Valid') { '' } else { ' (unsigned)' })
$rel = Invoke-Native { gh release view "v$version" --repo $Repository --json url }
if ($rel.Code -eq 0) {
    & gh release upload "v$version" @assets --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Release exists, but asset upload did not complete.' }
} else {
    & gh release create "v$version" @assets --repo $Repository --title $title --notes-file $releaseNotes --prerelease --verify-tag
    if ($LASTEXITCODE -ne 0) { throw 'Source was published, but release creation did not complete.' }
}
& gh release view "v$version" --repo $Repository --json url,assets,isPrerelease

