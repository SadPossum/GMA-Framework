param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [string] $ExpectedBranch = '',
    [string[]] $PathPrefix = @()
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

function Test-GmaGitSuccess {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [string] $WorkingDirectory = (Get-GmaCompositionRepositoryRoot)
    )

    & git -C $WorkingDirectory @Arguments *> $null
    return $LASTEXITCODE -eq 0
}

function Resolve-GmaDeclaredSubmoduleUrl {
    param([Parameter(Mandatory = $true)][string] $Url)

    if (-not ($Url.StartsWith('./', [System.StringComparison]::Ordinal) -or
        $Url.StartsWith('../', [System.StringComparison]::Ordinal))) {
        return $Url
    }

    # Git resolves relative submodule URLs against the superproject remote,
    # treating its repository name as a directory, rather than the checkout.
    $parentBranch = @(Invoke-GmaCompositionGitText -Arguments @('rev-parse', '--abbrev-ref', 'HEAD'))[0].Trim()
    $trackingRemote = @(& git -C (Get-GmaCompositionRepositoryRoot) config --get "branch.$parentBranch.remote" 2>$null)
    $remoteName = if ($LASTEXITCODE -eq 0 -and $trackingRemote.Count -eq 1) { $trackingRemote[0].Trim() } else { 'origin' }
    $parentUrl = if ($remoteName -eq '.') {
        Get-GmaCompositionRepositoryRoot
    }
    else {
        $parentRemotes = @(& git -C (Get-GmaCompositionRepositoryRoot) remote get-url $remoteName 2>$null)
        if ($LASTEXITCODE -eq 0 -and $parentRemotes.Count -eq 1) {
            $parentRemotes[0].Trim()
        }
        elseif ($remoteName -eq 'origin') {
            Get-GmaCompositionRepositoryRoot
        }
        else {
            throw "Could not resolve the composition tracking remote '$remoteName'."
        }
    }
    if ([System.IO.Path]::IsPathRooted($parentUrl)) {
        return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($parentUrl, $Url))
    }

    if ($parentUrl -notmatch '://' -and $parentUrl -match '^(?<prefix>[^/]+:)(?<path>.+)$') {
        $prefix = $Matches['prefix']
        $path = $Matches['path']
        $leadingSlash = if ($path.StartsWith('/', [System.StringComparison]::Ordinal)) { '/' } else { '' }
        $baseUri = [System.Uri]::new("https://relative.invalid/$($path.Trim('/'))/")
        $resolved = [System.Uri]::new($baseUri, $Url)
        return $prefix + $leadingSlash + [System.Uri]::UnescapeDataString($resolved.AbsolutePath.TrimStart('/'))
    }

    if ($parentUrl -match '://') {
        return [System.Uri]::new([System.Uri]::new($parentUrl.TrimEnd('/') + '/'), $Url).AbsoluteUri
    }

    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine(
        (Get-GmaCompositionRepositoryRoot), $parentUrl, $Url))
}

$submodules = @(Get-GmaCompositionSubmodules)
if ($PathPrefix.Count -gt 0) {
    $submodules = @($submodules | Where-Object {
        $candidatePath = $_.Path.Replace('\', '/')
        @($PathPrefix | Where-Object {
            $candidatePath.StartsWith($_.Replace('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
    })
}

if ($submodules.Count -eq 0) {
    Write-Host 'No matching submodules were found. Nothing to check.'
    return
}

$errors = [System.Collections.Generic.List[string]]::new()
$results = [System.Collections.Generic.List[object]]::new()
foreach ($submodule in $submodules) {
    $relativePath = $submodule.Path
    $submodulePath = Join-GmaCompositionPath $relativePath
    $branch = $submodule.Branch

    if ([string]::IsNullOrWhiteSpace($branch)) {
        $errors.Add("Submodule '$relativePath' does not declare a branch in .gitmodules.")
        continue
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedBranch) -and
        -not [string]::Equals($branch, $ExpectedBranch, [System.StringComparison]::Ordinal)) {
        $errors.Add("Submodule '$relativePath' tracks '$branch' in .gitmodules, expected '$ExpectedBranch'.")
    }

    if (-not (Test-Path -LiteralPath $submodulePath -PathType Container)) {
        $errors.Add("Submodule checkout missing at '$relativePath'. Initialize submodules first.")
        continue
    }

    if (-not (Test-GmaGitSuccess -Arguments @('diff', '--quiet', '--', $relativePath))) {
        $errors.Add("Submodule gitlink '$relativePath' has unstaged pointer changes in the composition repository.")
    }

    if (-not (Test-GmaGitSuccess -Arguments @('diff', '--cached', '--quiet', '--', $relativePath))) {
        $errors.Add("Submodule gitlink '$relativePath' has staged but uncommitted pointer changes in the composition repository.")
    }

    $dirtyRows = @(Invoke-GmaCompositionGitText -Arguments @('status', '--porcelain') -WorkingDirectory $submodulePath)
    if ($dirtyRows.Count -gt 0) {
        $errors.Add("Submodule '$relativePath' has uncommitted local changes.")
    }

    $currentCommit = @(Invoke-GmaCompositionGitText -Arguments @('rev-parse', 'HEAD') -WorkingDirectory $submodulePath)[0].Trim().ToLowerInvariant()
    $recordedCommit = @(Invoke-GmaCompositionGitText -Arguments @('rev-parse', ":$relativePath"))[0].Trim().ToLowerInvariant()
    # A locally repointed origin must not validate a gitlink belonging to a
    # different repository than the one committed in the composition manifest.
    $declaredUrl = Resolve-GmaDeclaredSubmoduleUrl -Url $submodule.Url
    $remoteRows = @(Invoke-GmaCompositionGitText -Arguments @('ls-remote', '--', $declaredUrl, "refs/heads/$branch") |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($remoteRows.Count -ne 1 -or $remoteRows[0] -notmatch '^(?<sha>[0-9a-fA-F]{40})\s+refs/heads/') {
        $errors.Add("Could not resolve declared branch '$branch' for submodule '$relativePath'.")
        continue
    }

    $remoteCommit = $Matches['sha'].ToLowerInvariant()
    if ($currentCommit -ne $remoteCommit) {
        $errors.Add("Submodule '$relativePath' is at $($currentCommit.Substring(0, 7)), but its declared source/$branch is $($remoteCommit.Substring(0, 7)).")
    }

    if ($recordedCommit -ne $remoteCommit) {
        $errors.Add("The composition repository records '$relativePath' at $($recordedCommit.Substring(0, 7)), but its declared source/$branch is $($remoteCommit.Substring(0, 7)).")
    }

    $results.Add([pscustomobject]@{
        Path = $relativePath
        Branch = $branch
        Current = $currentCommit.Substring(0, 7)
        Remote = $remoteCommit.Substring(0, 7)
        Recorded = $recordedCommit.Substring(0, 7)
    })
}

if ($results.Count -gt 0) {
    $results | Format-Table -AutoSize
}

if ($errors.Count -gt 0) {
    throw "Submodule head check failed:`n - $($errors -join "`n - ")"
}

Write-Host 'All selected submodule pointers match their configured remote branches.'
