param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_.-]*$')]
    [string] $Module,

    [Parameter(Mandatory = $true)]
    [ValidateSet('SqlServer', 'PostgreSql')]
    [string] $Provider,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string] $Name,

    [string] $Connection,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*DbContext$')]
    [string] $Context,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot $RepositoryRoot

function ConvertTo-GmaToolingKey {
    param([Parameter(Mandatory = $true)][string] $Value)

    return ([regex]::Replace($Value, '[^A-Za-z0-9]', '')).ToLowerInvariant()
}

function Get-GmaMigrationTargets {
    param([Parameter(Mandatory = $true)][string] $ProviderName)

    $moduleSources = [System.Collections.Generic.List[object]]::new()
    $applicationModulesRoot = Join-GmaCompositionPath 'src\Modules'
    if (Test-Path -LiteralPath $applicationModulesRoot -PathType Container) {
        foreach ($moduleRoot in Get-ChildItem -LiteralPath $applicationModulesRoot -Directory) {
            $moduleSources.Add([pscustomobject]@{
                ModuleRoot = $moduleRoot.FullName
                SourceRoot = $moduleRoot.FullName
                Alias = $moduleRoot.Name
            })
        }
    }

    $gmaModulesRoot = Join-GmaCompositionPath 'gma\modules'
    if (Test-Path -LiteralPath $gmaModulesRoot -PathType Container) {
        foreach ($moduleRoot in Get-ChildItem -LiteralPath $gmaModulesRoot -Directory) {
            $sourceRoot = Join-Path $moduleRoot.FullName 'src'
            if (Test-Path -LiteralPath $sourceRoot -PathType Container) {
                $moduleSources.Add([pscustomobject]@{
                    ModuleRoot = $moduleRoot.FullName
                    SourceRoot = $sourceRoot
                    Alias = $moduleRoot.Name
                })
            }
        }
    }

    $targets = [System.Collections.Generic.List[object]]::new()
    foreach ($source in $moduleSources) {
        $persistenceProjects = @(Get-ChildItem -LiteralPath $source.SourceRoot -Recurse -Filter '*.csproj' -File |
            Where-Object { $_.BaseName.EndsWith('.Persistence', [System.StringComparison]::Ordinal) })

        foreach ($persistenceProject in $persistenceProjects) {
            $projectPrefix = $persistenceProject.BaseName.Substring(
                0,
                $persistenceProject.BaseName.Length - '.Persistence'.Length)
            $migrationProjectName = "$projectPrefix.Persistence.${ProviderName}Migrations"
            $migrationProject = Get-ChildItem -LiteralPath $source.SourceRoot -Recurse -Filter "$migrationProjectName.csproj" -File |
                Select-Object -First 1
            if ($null -eq $migrationProject) {
                continue
            }

            $contextModuleName = ($projectPrefix -split '\.')[-1]
            $targets.Add([pscustomobject]@{
                ModuleRoot = $source.ModuleRoot
                SourceRoot = $source.SourceRoot
                Alias = $source.Alias
                ProjectPrefix = $projectPrefix
                ContextModuleName = $contextModuleName
                PersistenceRoot = $persistenceProject.Directory.FullName
                PersistenceProject = $persistenceProject.FullName
                MigrationRoot = $migrationProject.Directory.FullName
                MigrationProject = $migrationProject.FullName
                Keys = @(
                    (ConvertTo-GmaToolingKey -Value $source.Alias),
                    (ConvertTo-GmaToolingKey -Value $projectPrefix),
                    (ConvertTo-GmaToolingKey -Value $contextModuleName)
                ) | Sort-Object -Unique
            })
        }
    }

    return $targets.ToArray()
}

function Resolve-GmaMigrationTarget {
    param(
        [Parameter(Mandatory = $true)][string] $ModuleName,
        [Parameter(Mandatory = $true)][string] $ProviderName
    )

    $moduleKey = ConvertTo-GmaToolingKey -Value $ModuleName
    $matches = @(Get-GmaMigrationTargets -ProviderName $ProviderName |
        Where-Object { $_.Keys -contains $moduleKey })

    if ($matches.Count -eq 1) {
        return $matches[0]
    }

    if ($matches.Count -gt 1) {
        $prefixes = $matches.ProjectPrefix -join ', '
        throw "Module '$ModuleName' matched multiple $ProviderName migration targets: $prefixes. Use the full project prefix."
    }

    throw "Module '$ModuleName' with $ProviderName migrations was not found under src\Modules or gma\modules."
}

function Resolve-GmaDbContextName {
    param(
        [Parameter(Mandatory = $true)][string] $PersistenceRoot,
        [Parameter(Mandatory = $true)][string] $ModuleName,
        [string] $RequestedContext
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedContext)) {
        return $RequestedContext
    }

    $contextNames = @(Get-ChildItem -LiteralPath $PersistenceRoot -Filter '*.cs' -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\Migrations\\' } |
        ForEach-Object {
            $source = Get-Content -LiteralPath $_.FullName -Raw
            [regex]::Matches($source, '\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*DbContext)\b') |
                ForEach-Object { $_.Groups['name'].Value }
        } | Sort-Object -Unique)

    if ($contextNames.Count -eq 1) {
        return $contextNames[0]
    }

    $conventionalName = "${ModuleName}DbContext"
    if ($contextNames -contains $conventionalName) {
        return $conventionalName
    }

    throw "Could not determine DbContext for module '$ModuleName'. Found: $($contextNames -join ', '). Pass -Context explicitly."
}

Invoke-GmaCompositionDotNet -Arguments @('tool', 'restore')

$target = Resolve-GmaMigrationTarget -ModuleName $Module -ProviderName $Provider
$contextName = Resolve-GmaDbContextName `
    -PersistenceRoot $target.PersistenceRoot `
    -ModuleName $target.ContextModuleName `
    -RequestedContext $Context

Invoke-GmaCompositionDotNet -Arguments @('build', $target.MigrationProject, '--no-restore')

$arguments = @(
    'ef', 'migrations', 'add', $Name,
    '--no-build',
    '--project', $target.MigrationProject,
    '--startup-project', $target.MigrationProject,
    '--context', $contextName,
    '--output-dir', 'Migrations',
    '--', '--provider', $Provider
)

if (-not [string]::IsNullOrWhiteSpace($Connection)) {
    $arguments += @('--connection', $Connection)
}

Invoke-GmaCompositionDotNet -Arguments $arguments
