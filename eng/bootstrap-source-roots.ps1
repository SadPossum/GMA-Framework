[CmdletBinding(SupportsShouldProcess = $true)]
param([switch] $Force)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $repositoryRoot 'Gma.SourceRoots.props.example'
$target = Join-Path $repositoryRoot 'Gma.SourceRoots.props'

if (-not (Test-Path -LiteralPath $source)) {
    throw "Missing source-root example file: $source"
}

$sourceLines = [System.IO.File]::ReadAllLines($source)
if (Test-Path -LiteralPath $target) {
    $targetLines = [System.IO.File]::ReadAllLines($target)
    if ([string]::Join("`n", $sourceLines) -eq [string]::Join("`n", $targetLines)) {
        Write-Host "Gma.SourceRoots.props already matches the example."
        return
    }

    if (-not $Force) {
        throw "Gma.SourceRoots.props already exists with different contents. Use -Force to refresh it from the example."
    }
}

$action = if (Test-Path -LiteralPath $target) { 'Overwrite' } else { 'Create' }
if ($PSCmdlet.ShouldProcess($target, "$action local source-root configuration")) {
    [System.IO.File]::WriteAllLines($target, $sourceLines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "$action local source-root configuration: $target"
}
