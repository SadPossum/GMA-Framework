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
    $remoteRows = @(Invoke-GmaCompositionGitText -Arguments @('ls-remote', 'origin', "refs/heads/$branch") -WorkingDirectory $submodulePath |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($remoteRows.Count -ne 1 -or $remoteRows[0] -notmatch '^(?<sha>[0-9a-fA-F]{40})\s+refs/heads/') {
        $errors.Add("Could not resolve origin/$branch for submodule '$relativePath'.")
        continue
    }

    $remoteCommit = $Matches['sha'].ToLowerInvariant()
    if ($currentCommit -ne $remoteCommit) {
        $errors.Add("Submodule '$relativePath' is at $($currentCommit.Substring(0, 7)), but origin/$branch is $($remoteCommit.Substring(0, 7)).")
    }

    if ($recordedCommit -ne $remoteCommit) {
        $errors.Add("The composition repository records '$relativePath' at $($recordedCommit.Substring(0, 7)), but origin/$branch is $($remoteCommit.Substring(0, 7)).")
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
