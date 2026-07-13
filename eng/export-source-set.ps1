param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [string] $OutputPath = 'artifacts/gma-source-set.json',
    [switch] $RequireClean
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

function Get-GmaRepositoryEntry {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [string] $Url = '',
        [string] $ConfiguredBranch = ''
    )

    $status = @(Invoke-GmaCompositionGitText -Arguments @(
        'status', '--porcelain=v1', '--untracked-files=all') -WorkingDirectory $Path)
    $branchRows = @(& git -C $Path branch --show-current 2>$null)
    $branch = if ($LASTEXITCODE -eq 0 -and $branchRows.Count -gt 0) { $branchRows[0].Trim() } else { '' }

    return [ordered]@{
        path = $RelativePath.Replace('\', '/')
        url = $Url
        commit = @(Invoke-GmaCompositionGitText -Arguments @('rev-parse', 'HEAD') -WorkingDirectory $Path)[0].Trim()
        branch = $branch
        configuredBranch = $ConfiguredBranch
        dirty = @($status | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -gt 0
    }
}

$repositories = [System.Collections.Generic.List[object]]::new()
$repositories.Add((Get-GmaRepositoryEntry -Path (Get-GmaCompositionRepositoryRoot) -RelativePath '.'))
foreach ($submodule in Get-GmaCompositionSubmodules) {
    $fullPath = Join-GmaCompositionPath $submodule.Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Submodule '$($submodule.Path)' is not initialized."
    }

    $repositories.Add((Get-GmaRepositoryEntry `
        -Path $fullPath `
        -RelativePath $submodule.Path `
        -Url $submodule.Url `
        -ConfiguredBranch $submodule.Branch))
}

$dirtyRepositories = @($repositories | Where-Object { $_.dirty })
if ($RequireClean -and $dirtyRepositories.Count -gt 0) {
    throw "A release source set must be clean. Dirty repositories: $($dirtyRepositories.path -join ', ')."
}

$globalJsonPath = Join-GmaCompositionPath 'global.json'
$packagesPath = Join-GmaCompositionPath 'Directory.Packages.props'
if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $packagesPath -PathType Leaf)) {
    throw 'A source set requires global.json and Directory.Packages.props at the composition root.'
}

$manifest = [ordered]@{
    schemaVersion = 2
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    rootCommit = $repositories[0].commit
    sdkVersion = (Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
    centralPackagesSha256 = (Get-FileHash -LiteralPath $packagesPath -Algorithm SHA256).Hash.ToLowerInvariant()
    repositories = $repositories.ToArray()
}

$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-GmaCompositionPath $OutputPath))
}
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllText(
    $resolvedOutputPath,
    ($manifest | ConvertTo-Json -Depth 6) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "sourceSet=$resolvedOutputPath"
