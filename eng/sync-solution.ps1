param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $Solution,

    [string[]] $ProjectRoots = @('src', 'tests', 'gma'),
    [string[]] $OperationalRoots = @('docs', 'eng', '.github', 'requests'),
    [string[]] $SolutionItems = @(
        '.config/dotnet-tools.json',
        '.editorconfig',
        '.gitattributes',
        '.gitignore',
        '.gitmodules',
        'Directory.Build.props',
        'Directory.Packages.props',
        'global.json',
        'Gma.SourceRoots.props.example',
        'LICENSE',
        'nuget.config',
        'README.md'
    ),
    [string] $HostProjectPattern = '\.Host(\.|$)',
    [bool] $IncludeSourceMarkdown = $true,
    [switch] $Check
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

function Add-GmaSolutionEntry {
    param(
        [Parameter(Mandatory = $true)][hashtable] $Folders,
        [Parameter(Mandatory = $true)][string] $Folder,
        [Parameter(Mandatory = $true)][ValidateSet('Project', 'File')][string] $Kind,
        [Parameter(Mandatory = $true)][string] $Path
    )

    if (-not $Folders.ContainsKey($Folder)) {
        $Folders[$Folder] = [ordered]@{
            Projects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            Files = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        }
    }

    $normalizedPath = $Path.Replace('\', '/')
    if ($Kind -eq 'Project') {
        [void]$Folders[$Folder].Projects.Add($normalizedPath)
    }
    else {
        [void]$Folders[$Folder].Files.Add($normalizedPath)
    }
}

function Get-GmaOrdinalSortedStrings {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Values)

    $sorted = [System.Collections.Generic.List[string]]::new()
    foreach ($value in $Values) {
        [void]$sorted.Add($value)
    }

    $sorted.Sort([System.StringComparer]::Ordinal)
    return $sorted
}

function Get-GmaProjectSolutionFolder {
    param([Parameter(Mandatory = $true)][string] $RelativePath)

    $normalizedPath = $RelativePath.Replace('\', '/')
    $segments = $normalizedPath.Split('/')
    if ($segments.Count -ge 3 -and $segments[0] -eq 'src' -and $segments[1] -eq 'Modules') {
        $suffix = if ($segments.Count -ge 4 -and $segments[3] -eq 'tests') { '/tests' } else { '' }
        return "/src/Modules/$($segments[2])$suffix/"
    }

    if ($segments.Count -ge 2 -and $segments[0] -eq 'src' -and $segments[1] -eq 'Adapters') {
        if ($segments.Count -ge 3 -and $segments[2] -eq 'tests') {
            return '/src/Adapters/tests/'
        }

        return '/src/Adapters/'
    }

    if ($segments.Count -ge 2 -and $segments[0] -eq 'src' -and $segments[1] -eq 'Shared') {
        return '/src/Shared/'
    }

    if ($segments.Count -ge 2 -and $segments[0] -eq 'src' -and
        ($segments[1] -eq 'Hosts' -or $segments[1] -match $HostProjectPattern)) {
        return '/src/Hosts/'
    }

    if ($segments[0] -eq 'src') {
        return '/src/'
    }

    if ($segments[0] -eq 'tests') {
        return '/tests/'
    }

    if ($segments[0] -eq 'gma') {
        $projectDirectory = Split-Path $normalizedPath -Parent
        $ownerDirectory = Split-Path $projectDirectory -Parent
        if (-not [string]::IsNullOrWhiteSpace($ownerDirectory)) {
            return "/$($ownerDirectory.Replace('\', '/'))/"
        }

        return '/gma/'
    }

    return '/Other/'
}

function ConvertTo-GmaSolutionXml {
    param([Parameter(Mandatory = $true)][hashtable] $Folders)

    Add-Type -AssemblyName System.Xml.Linq
    $solutionElement = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('Solution'))
    foreach ($folderName in (Get-GmaOrdinalSortedStrings -Values @($Folders.Keys))) {
        $folder = $Folders[$folderName]
        if ($folder.Projects.Count -eq 0 -and $folder.Files.Count -eq 0) {
            continue
        }

        $folderElement = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('Folder'))
        $folderElement.SetAttributeValue([System.Xml.Linq.XName]::Get('Name'), $folderName)
        foreach ($file in (Get-GmaOrdinalSortedStrings -Values @($folder.Files))) {
            $fileElement = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('File'))
            $fileElement.SetAttributeValue([System.Xml.Linq.XName]::Get('Path'), $file)
            $folderElement.Add($fileElement)
        }

        foreach ($project in (Get-GmaOrdinalSortedStrings -Values @($folder.Projects))) {
            $projectElement = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]::Get('Project'))
            $projectElement.SetAttributeValue([System.Xml.Linq.XName]::Get('Path'), $project)
            $folderElement.Add($projectElement)
        }

        $solutionElement.Add($folderElement)
    }

    $document = [System.Xml.Linq.XDocument]::new($solutionElement)
    return $document.ToString() + [Environment]::NewLine
}

$folders = @{}
foreach ($projectRoot in $ProjectRoots) {
    $absoluteRoot = Join-GmaCompositionPath $projectRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
        continue
    }

    foreach ($project in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.csproj' -File) {
        $relativePath = Get-GmaCompositionRelativePath `
            -BasePath (Get-GmaCompositionRepositoryRoot) `
            -TargetPath $project.FullName
        if ($relativePath -match '(^|[\\/])(\.tmp|bin|obj)([\\/]|$)') {
            continue
        }

        Add-GmaSolutionEntry `
            -Folders $folders `
            -Folder (Get-GmaProjectSolutionFolder -RelativePath $relativePath) `
            -Kind Project `
            -Path $relativePath
    }
}

$operationalExtensions = @('.md', '.ps1', '.yml', '.yaml', '.http')
foreach ($operationalRoot in $OperationalRoots) {
    $absoluteRoot = Join-GmaCompositionPath $operationalRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File |
        Where-Object { $operationalExtensions -contains $_.Extension }) {
        $relativePath = Get-GmaCompositionRelativePath `
            -BasePath (Get-GmaCompositionRepositoryRoot) `
            -TargetPath $file.FullName
        $directory = Split-Path $relativePath -Parent
        Add-GmaSolutionEntry `
            -Folders $folders `
            -Folder "/$($directory.Replace('\', '/'))/" `
            -Kind File `
            -Path $relativePath
    }
}

if ($IncludeSourceMarkdown) {
    $sourceRoot = Join-GmaCompositionPath 'src'
    if (Test-Path -LiteralPath $sourceRoot -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.md' -File) {
            $relativePath = Get-GmaCompositionRelativePath `
                -BasePath (Get-GmaCompositionRepositoryRoot) `
                -TargetPath $file.FullName
            $directory = Split-Path $relativePath -Parent
            Add-GmaSolutionEntry `
                -Folders $folders `
                -Folder "/$($directory.Replace('\', '/'))/" `
                -Kind File `
                -Path $relativePath
        }
    }
}

foreach ($solutionItem in $SolutionItems) {
    if (Test-Path -LiteralPath (Join-GmaCompositionPath $solutionItem) -PathType Leaf) {
        Add-GmaSolutionEntry -Folders $folders -Folder '/Solution Items/' -Kind File -Path $solutionItem
    }
}

$solutionPath = if ([System.IO.Path]::IsPathRooted($Solution)) {
    [System.IO.Path]::GetFullPath($Solution)
}
else {
    [System.IO.Path]::GetFullPath((Join-GmaCompositionPath $Solution))
}
$expectedContent = ConvertTo-GmaSolutionXml -Folders $folders

if ($Check) {
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        throw "Solution '$solutionPath' does not exist. Run the solution synchronization tool without -Check."
    }

    $actualContent = [System.IO.File]::ReadAllText($solutionPath)
    $normalize = {
        param([string] $Value)
        return $Value.Replace("`r`n", "`n").TrimEnd() + "`n"
    }
    if ((& $normalize $actualContent) -ne (& $normalize $expectedContent)) {
        throw "Solution '$solutionPath' is out of date. Run the solution synchronization tool without -Check."
    }

    Write-Host "Solution is synchronized: $solutionPath"
    return
}

[System.IO.File]::WriteAllText(
    $solutionPath,
    $expectedContent,
    [System.Text.UTF8Encoding]::new($false))
Write-Host "Solution synchronized: $solutionPath"
