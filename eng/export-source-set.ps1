param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [string] $OutputPath = 'artifacts/gma-source-set.json',
    [switch] $RequireClean,
    [switch] $Recursive
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

function Get-GmaDeclaredSubmodules {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryPath,
        [Parameter(Mandatory = $true)][string] $ParentRelativePath
    )

    $gitmodulesPath = Join-Path $RepositoryPath '.gitmodules'
    if (-not (Test-Path -LiteralPath $gitmodulesPath -PathType Leaf)) {
        return @()
    }

    $pathRows = @(Invoke-GmaCompositionGitText `
        -WorkingDirectory $RepositoryPath `
        -Arguments @(
            'config',
            '--file',
            $gitmodulesPath,
            '--get-regexp',
            '^submodule\..*\.path$'
        ))
    $submodules = [System.Collections.Generic.List[object]]::new()
    $repositoryPrefix =
        [System.IO.Path]::GetFullPath($RepositoryPath).TrimEnd('\', '/') +
        [System.IO.Path]::DirectorySeparatorChar

    foreach ($pathRow in $pathRows) {
        if ($pathRow -notmatch
            '^submodule\.(?<name>.+)\.path\s+(?<path>.+)$') {
            throw "Could not parse .gitmodules path row '$pathRow'."
        }

        $name = $Matches['name']
        $path = $Matches['path']
        if ([System.IO.Path]::IsPathRooted($path)) {
            throw "Submodule '$name' uses rooted path '$path'."
        }

        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $RepositoryPath $path))
        if (-not $fullPath.StartsWith(
                $repositoryPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Submodule '$name' resolves outside '$RepositoryPath'."
        }

        $urlRows = @(Invoke-GmaCompositionGitText `
            -WorkingDirectory $RepositoryPath `
            -Arguments @(
                'config',
                '--file',
                $gitmodulesPath,
                '--get',
                "submodule.$name.url"
            ))
        $branchRows = @(& git -C $RepositoryPath config `
            --file $gitmodulesPath `
            --get "submodule.$name.branch" 2>$null)
        $branch = if ($LASTEXITCODE -eq 0 -and $branchRows.Count -gt 0) {
            $branchRows[0].Trim()
        }
        else {
            ''
        }
        $relativePath = if ($ParentRelativePath -eq '.') {
            $path
        }
        else {
            "$ParentRelativePath/$path"
        }

        $submodules.Add([pscustomobject]@{
            Path = $relativePath.Replace('\', '/')
            FullPath = $fullPath
            Url = $urlRows[0].Trim()
            Branch = $branch
        })
    }

    return @($submodules | Sort-Object Path)
}

function Get-GmaSourceSubmodules {
    $submodules = [System.Collections.Generic.List[object]]::new()
    $pending = [System.Collections.Generic.Queue[object]]::new()
    foreach ($submodule in Get-GmaDeclaredSubmodules `
        -RepositoryPath (Get-GmaCompositionRepositoryRoot) `
        -ParentRelativePath '.') {
        $pending.Enqueue($submodule)
    }

    while ($pending.Count -gt 0) {
        $submodule = $pending.Dequeue()
        if (-not (Test-Path -LiteralPath $submodule.FullPath -PathType Container)) {
            throw "Submodule '$($submodule.Path)' is not initialized."
        }

        $submodules.Add($submodule)
        if ($Recursive) {
            foreach ($nested in Get-GmaDeclaredSubmodules `
                -RepositoryPath $submodule.FullPath `
                -ParentRelativePath $submodule.Path) {
                $pending.Enqueue($nested)
            }
        }
    }

    return @($submodules | Sort-Object Path)
}

$repositories = [System.Collections.Generic.List[object]]::new()
$repositories.Add((Get-GmaRepositoryEntry -Path (Get-GmaCompositionRepositoryRoot) -RelativePath '.'))
foreach ($submodule in Get-GmaSourceSubmodules) {
    $repositories.Add((Get-GmaRepositoryEntry `
        -Path $submodule.FullPath `
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
