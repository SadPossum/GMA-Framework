param(
    [switch] $NoBuild,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

Invoke-GmaCompositionDotNet -Arguments @('tool', 'restore')

$migrationSearchRoots = @('src\Modules', 'gma\modules') |
    ForEach-Object { Join-GmaCompositionPath $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Container }
$migrationProjects = @($migrationSearchRoots |
    ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -Filter '*.csproj' -File } |
    Where-Object {
        $_.BaseName.EndsWith('.Persistence.SqlServerMigrations', [System.StringComparison]::Ordinal) -or
        $_.BaseName.EndsWith('.Persistence.PostgreSqlMigrations', [System.StringComparison]::Ordinal)
    } |
    Sort-Object FullName)

if ($migrationProjects.Count -eq 0) {
    throw 'No provider migration projects were found under src\Modules or gma\modules.'
}

foreach ($project in $migrationProjects) {
    $relativeProject = Get-GmaCompositionRelativePath `
        -BasePath (Get-GmaCompositionRepositoryRoot) `
        -TargetPath $project.FullName
    Write-Host "Checking migration drift for $relativeProject"

    if (-not $NoBuild) {
        Invoke-GmaCompositionDotNet -Arguments @('build', $project.FullName, '--no-restore')
    }

    $arguments = @(
        'tool', 'run', 'dotnet-ef',
        'migrations', 'has-pending-model-changes',
        '--project', $project.FullName,
        '--startup-project', $project.FullName
    )
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    Invoke-GmaCompositionDotNet -Arguments $arguments
}

Write-Host 'Migration drift checks passed.'
