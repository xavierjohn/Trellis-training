[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Assert-TrainingContract([bool] $condition, [string] $message) {
    if (-not $condition) { throw $message }
}

$legacyReferences = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force -Filter 'trellis-*.md' |
    Where-Object { $_.FullName -match '\\\.github\\' })
Assert-TrainingContract ($legacyReferences.Count -eq 0) `
    "Reference documents must not live in any .github folder; use AgentDocs instead: $($legacyReferences.FullName -join ', ')"

foreach ($snapshot in 'before', 'after') {
    $project = Join-Path $root "$snapshot\OrderManagement"
    $packages = [xml](Get-Content -LiteralPath (Join-Path $project 'Directory.Packages.props') -Raw)
    Assert-TrainingContract ($packages.Project.PropertyGroup.TrellisVersion -eq '3.0.0-alpha.557') `
        "$snapshot must use the course's Trellis alpha.557 baseline."
    Assert-TrainingContract (Test-Path (Join-Path $project 'AGENTS.md')) "$snapshot must have an AGENTS.md entrypoint."
    Assert-TrainingContract (Test-Path (Join-Path $project '.agentdocs\README.md')) "$snapshot must include version-aligned AgentDocs."
    Assert-TrainingContract (Test-Path (Join-Path $project '.agentdocs\packages\trellis.core\trellis\trellis-start-here.md')) `
        "$snapshot must include Core's required router, not merely an index with missing links."
}

$materials = @(
    Get-Item (Join-Path $root 'README.md')
    Get-ChildItem (Join-Path $root 'docs') -Filter '*.md'
    Get-ChildItem (Join-Path $root 'specs') -Filter '*.md'
)
foreach ($file in $materials) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    Assert-TrainingContract ($text -notmatch 'Trellis\.AspTemplate|--authorName|\.github/trellis-api-') `
        "$($file.Name) contains obsolete template or guidance instructions."
    Assert-TrainingContract ($text -notmatch 'new Error\.Conflict\(null,|Error\.Unavailable\("') `
        "$($file.Name) contains obsolete error constructor arguments."
}

foreach ($guide in 'training-lab.md', 'training-lab-worker.md', 'training-lab-url-shortener.md') {
    $text = Get-Content -LiteralPath (Join-Path $root "docs\$guide") -Raw
    Assert-TrainingContract ($text.Contains('Trellis.Asp.Templates@1.0.151-alpha')) "$guide must pin the published course template."
    Assert-TrainingContract ($text.Contains('AGENTS.md')) "$guide must start agents at the generated instruction entrypoint."
}

$urlSpec = Get-Content -LiteralPath (Join-Path $root 'specs\url-shortener.md') -Raw
Assert-TrainingContract (-not $urlSpec.Contains('.WithVersionedRoute()')) `
    'The unversioned URL-shortener lab must not require the optional versioning SDK.'
Assert-TrainingContract ($urlSpec.Contains('TryCreatePageRequest')) 'The URL-shortener lab must validate raw pagination input.'
foreach ($file in 'url-shortener.md', 'coverage-checklist-url-shortener.md') {
    $text = Get-Content -LiteralPath (Join-Path $root "specs\$file") -Raw
    Assert-TrainingContract ($text -match '(?i)retain.*(record|key|tombstone)') "$file must retain deleted-link keys."
    Assert-TrainingContract ($text -notmatch '(?i)(byte-equivalent|byte-for-byte).*original.*(201|creation|response)') `
        "$file must describe current-view replay, not an original response snapshot."
    Assert-TrainingContract ($text -notmatch '(?i)Cascade.*Link.*IdempotencyRecord') `
        "$file must not cascade-delete idempotency records."
}

$workerSpec = Get-Content -LiteralPath (Join-Path $root 'specs\subscription-reminder-worker.md') -Raw
foreach ($api in 'UseWorkerActor', 'Classify()', 'TryInsertUniqueAsync', 'WorkerHarness', 'FailAfterCommit') {
    Assert-TrainingContract ($workerSpec.Contains($api)) "The worker lab must teach the current $api pattern."
}

Write-Host 'Training contracts passed.'
