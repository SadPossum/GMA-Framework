Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:GmaCompositionRepositoryRoot = $null

function Initialize-GmaCompositionTooling {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        throw 'RepositoryRoot is required.'
    }

    $script:GmaCompositionRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
}

function Get-GmaCompositionRepositoryRoot {
    if ([string]::IsNullOrWhiteSpace($script:GmaCompositionRepositoryRoot)) {
        throw 'Composition tooling was not initialized.'
    }

    return $script:GmaCompositionRepositoryRoot
}

function Join-GmaCompositionPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return Join-Path (Get-GmaCompositionRepositoryRoot) $Path
}

function Resolve-GmaCompositionDotNet {
    $candidates = @()
    $resolutionErrors = @()

    if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET)) {
        $candidates += $env:GMA_DOTNET
    }

    $candidates += 'dotnet'

    foreach ($candidate in $candidates) {
        try {
            Push-Location -LiteralPath (Get-GmaCompositionRepositoryRoot)
            try {
                $version = & $candidate --version 2>$null
            }
            finally {
                Pop-Location
            }

            if ($LASTEXITCODE -ne 0) {
                $resolutionErrors += "Candidate '$candidate' exited with code $LASTEXITCODE."
                continue
            }

            if ($version -match '^10\.') {
                return $candidate
            }

            $resolutionErrors += "Candidate '$candidate' is version '$version'."
        }
        catch {
            if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET) -and $candidate -eq $env:GMA_DOTNET) {
                throw
            }

            $resolutionErrors += "Candidate '$candidate' failed: $($_.Exception.Message)"
        }
    }

    $details = if ($resolutionErrors.Count -gt 0) {
        " Tried candidates: $($resolutionErrors -join ' ')"
    }
    else {
        ''
    }

    throw "Could not resolve a .NET 10 SDK. Set GMA_DOTNET or install the .NET 10 SDK.$details"
}

function Invoke-GmaCompositionDotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = (Get-GmaCompositionRepositoryRoot)
    )

    $dotnet = Resolve-GmaCompositionDotNet
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $dotnet @Arguments
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-GmaCompositionRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BasePath,

        [Parameter(Mandatory = $true)]
        [string] $TargetPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') +
        [System.IO.Path]::DirectorySeparatorChar
    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not $targetFullPath.StartsWith($baseFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$targetFullPath' is outside '$baseFullPath'."
    }

    return $targetFullPath.Substring($baseFullPath.Length)
}

function Invoke-GmaCompositionGitText {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = (Get-GmaCompositionRepositoryRoot)
    )

    $output = & git -C $WorkingDirectory @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git -C '$WorkingDirectory' $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    return @($output)
}

function Get-GmaCompositionSubmodules {
    $gitmodulesPath = Join-GmaCompositionPath '.gitmodules'
    if (-not (Test-Path -LiteralPath $gitmodulesPath -PathType Leaf)) {
        return @()
    }

    $pathRows = @(Invoke-GmaCompositionGitText -Arguments @(
        'config', '--file', $gitmodulesPath, '--get-regexp', '^submodule\..*\.path$'))
    $submodules = [System.Collections.Generic.List[object]]::new()

    foreach ($pathRow in $pathRows) {
        if ($pathRow -notmatch '^submodule\.(?<name>.+)\.path\s+(?<path>.+)$') {
            throw "Could not parse .gitmodules path row '$pathRow'."
        }

        $name = $Matches['name']
        $path = $Matches['path']
        $urlRows = @(Invoke-GmaCompositionGitText -Arguments @(
            'config', '--file', $gitmodulesPath, '--get', "submodule.$name.url"))
        $branchRows = @(& git -C (Get-GmaCompositionRepositoryRoot) config --file $gitmodulesPath --get "submodule.$name.branch" 2>$null)
        $branch = if ($LASTEXITCODE -eq 0 -and $branchRows.Count -gt 0) { $branchRows[0].Trim() } else { '' }

        $submodules.Add([pscustomobject]@{
            Name = $name
            Path = $path
            Url = $urlRows[0].Trim()
            Branch = $branch
        })
    }

    return @($submodules | Sort-Object Path)
}
