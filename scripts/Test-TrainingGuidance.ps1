[CmdletBinding()]
param(
    [ValidateSet('before', 'after', 'both')]
    [string] $Snapshot = 'both',
    [switch] $Sync
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Checked([string] $executable, [string[]] $arguments) {
    & $executable @arguments
    if ($LASTEXITCODE -ne 0) { throw "$executable $($arguments -join ' ') failed ($LASTEXITCODE)." }
}

$snapshots = if ($Snapshot -eq 'both') { @('before', 'after') } else { @($Snapshot) }
foreach ($name in $snapshots) {
    $source = Join-Path $root "$name\OrderManagement"
    $export = Join-Path ([IO.Path]::GetTempPath()) "trellis-training-guidance-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($export) | Out-Null
    try {
        $prefix = "$name/OrderManagement/"
        $files = & git -C $root ls-files --cached --others --exclude-standard -- "$name\OrderManagement"
        if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate snapshot files.' }
        foreach ($file in $files) {
            if (-not $file.StartsWith($prefix, [StringComparison]::Ordinal)) {
                throw "Unexpected snapshot path: $file"
            }
            $relative = $file.Substring($prefix.Length).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $original = Join-Path $source $relative
            if (-not (Test-Path -LiteralPath $original -PathType Leaf)) { continue }
            $target = Join-Path $export $relative
            [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
            Copy-Item -LiteralPath $original -Destination $target
        }

        Push-Location $export
        try {
            Invoke-Checked git @('init', '--quiet')
            Invoke-Checked dotnet @('restore', 'OrderManagement.slnx', '--verbosity', 'quiet')
            Invoke-Checked dotnet @('tool', 'restore', '--verbosity', 'quiet')
            if ($Sync) {
                if (-not (Test-Path -LiteralPath '.agentdocs\agent-context.json')) {
                    Invoke-Checked dotnet @('agentdocs', 'init', 'OrderManagement.slnx')
                }
                Invoke-Checked dotnet @('agentdocs', 'sync')
            }
            Invoke-Checked dotnet @('agentdocs', 'check', '--strict')
        }
        finally { Pop-Location }

        if ($Sync) {
            $guidance = Join-Path $export '.agentdocs'
            foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source '.agentdocs') -File -Recurse -Force) {
                $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
                if (-not (Test-Path -LiteralPath (Join-Path $export $relative))) {
                    Remove-Item -LiteralPath $file.FullName
                }
            }
            foreach ($file in Get-ChildItem -LiteralPath $guidance -File -Recurse -Force) {
                $target = Join-Path $source ([IO.Path]::GetRelativePath($export, $file.FullName))
                [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
            foreach ($relative in @('AGENTS.md', '.github\copilot-instructions.md')) {
                $file = Join-Path $export $relative
                if (Test-Path -LiteralPath $file) {
                    $target = Join-Path $source $relative
                    [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
                    Copy-Item -LiteralPath $file -Destination $target -Force
                }
            }
        }
        Write-Host "$name snapshot guidance passed."
    }
    finally { Remove-Item -LiteralPath $export -Recurse -Force }
}
