param(
    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot (Split-Path $PSScriptRoot -Parent)

$arguments = @(
    'test',
    (Join-GmaCompositionPath 'Gma.Framework.slnx'),
    '--filter',
    'Category!=Docker',
    '--logger',
    'console;verbosity=minimal',
    '-m:1',
    '-nr:false'
)

if ($NoBuild) {
    $arguments += '--no-build'
}

$arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Invoke-GmaCompositionDotNet -Arguments $arguments
