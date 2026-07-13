param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [switch] $SkipRestore,
    [switch] $SkipBuild,
    [string[]] $PathPrefix = @('gma/framework', 'gma/modules/')
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

function ConvertTo-GmaSolutionPath {
    param([Parameter(Mandatory = $true)][string] $Path)
    return $Path.Replace('\', '/')
}

function Get-GmaPackageRequiredPaths {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $requiredPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($relativeRoot in @('docs', 'eng', 'src', 'tests', '.github')) {
        $absoluteRoot = Join-Path $PackageRoot $relativeRoot
        if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
            continue
        }

        foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                ($_.Extension -in @('.csproj', '.md', '.ps1', '.yml', '.yaml'))
            }) {
            $requiredPaths.Add((Get-GmaCompositionRelativePath -BasePath $PackageRoot -TargetPath $file.FullName).Replace('\', '/'))
        }
    }

    return @($requiredPaths | Sort-Object -Unique)
}

$selectedSubmodules = @(Get-GmaCompositionSubmodules | Where-Object {
    $candidatePath = $_.Path.Replace('\', '/')
    @($PathPrefix | Where-Object {
        $candidatePath.StartsWith($_.Replace('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -gt 0
})
if ($selectedSubmodules.Count -eq 0) {
    throw 'No matching source packages were found in .gitmodules.'
}

$allowedFolders = @('/.github/', '/Solution Items/', '/docs/', '/eng/', '/src/', '/tests/')
$allowedRootFiles = @(
    '.editorconfig', '.gitattributes', '.gitignore', 'Directory.Build.props',
    'Directory.Packages.props', 'global.json', 'Gma.SourceRoots.props.example',
    'LICENSE', 'nuget.config', 'README.md'
)
$errors = [System.Collections.Generic.List[string]]::new()
$packages = [System.Collections.Generic.List[object]]::new()

foreach ($submodule in $selectedSubmodules) {
    $packageRoot = Join-GmaCompositionPath $submodule.Path
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
        $errors.Add("Source package '$($submodule.Path)' is not initialized.")
        continue
    }

    $solutions = @(Get-ChildItem -LiteralPath $packageRoot -Filter '*.slnx' -File)
    if ($solutions.Count -ne 1) {
        $errors.Add("Source package '$($submodule.Path)' must contain exactly one root .slnx file; found $($solutions.Count).")
        continue
    }

    $solution = $solutions[0]
    try {
        [xml] $solutionXml = Get-Content -LiteralPath $solution.FullName -Raw
    }
    catch {
        $errors.Add("$($submodule.Path)/$($solution.Name) is not valid XML: $($_.Exception.Message)")
        continue
    }

    $entryPaths = @($solutionXml.SelectNodes('//*[@Path]') |
        ForEach-Object { ConvertTo-GmaSolutionPath -Path $_.GetAttribute('Path') })
    $duplicateEntries = @($entryPaths | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    foreach ($duplicate in $duplicateEntries) {
        $errors.Add("$($submodule.Path)/$($solution.Name) lists '$duplicate' more than once.")
    }

    foreach ($folder in $solutionXml.SelectNodes('//Folder')) {
        $folderName = $folder.GetAttribute('Name')
        $allowed = @($allowedFolders | Where-Object {
            $folderName -eq $_ -or $folderName.StartsWith($_, [System.StringComparison]::Ordinal)
        }).Count -gt 0
        if (-not $allowed) {
            $errors.Add("$($submodule.Path)/$($solution.Name) contains non-package-local folder '$folderName'.")
        }
    }

    foreach ($entryPath in $entryPaths) {
        if ($entryPath.StartsWith('../', [System.StringComparison]::Ordinal) -or
            $entryPath.StartsWith('/', [System.StringComparison]::Ordinal)) {
            $errors.Add("$($submodule.Path)/$($solution.Name) lists non-local entry '$entryPath'.")
            continue
        }

        if ($allowedRootFiles -notcontains $entryPath) {
            $firstSegment = ($entryPath -split '/', 2)[0]
            if ($allowedFolders -notcontains "/$firstSegment/") {
                $errors.Add("$($submodule.Path)/$($solution.Name) lists '$entryPath' outside allowed package folders.")
            }
        }

        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $entryPath))) {
            $errors.Add("$($submodule.Path)/$($solution.Name) lists missing path '$entryPath'.")
        }
    }

    foreach ($requiredPath in Get-GmaPackageRequiredPaths -PackageRoot $packageRoot) {
        if ($entryPaths -notcontains $requiredPath) {
            $errors.Add("$($submodule.Path)/$($solution.Name) does not list required path '$requiredPath'.")
        }
    }

    $packages.Add([pscustomobject]@{
        Path = $submodule.Path
        Solution = $solution.FullName
    })
}

if ($errors.Count -gt 0) {
    throw "Source package checks failed:`n - $($errors -join "`n - ")"
}

foreach ($package in $packages) {
    if (-not $SkipRestore) {
        Invoke-GmaCompositionDotNet -Arguments @('restore', $package.Solution)
    }

    if (-not $SkipBuild) {
        Invoke-GmaCompositionDotNet -Arguments @('build', $package.Solution, '--no-restore', '-m:1')
    }
}

Write-Host 'Source-package checks passed.'
