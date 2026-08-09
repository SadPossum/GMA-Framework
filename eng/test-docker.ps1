param(
    [switch] $NoBuild,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotNetArguments
)

. (Join-Path $PSScriptRoot 'composition-common.ps1')
Initialize-GmaCompositionTooling -RepositoryRoot (Split-Path $PSScriptRoot -Parent)

$previousRequireDockerTests = $env:GMA_REQUIRE_DOCKER_TESTS
$env:GMA_REQUIRE_DOCKER_TESTS = 'true'

try {
    $arguments = @(
        'test',
        (Join-GmaCompositionPath 'tests\Gma.Framework.Tests\Gma.Framework.Tests.csproj'),
        '--filter',
        'Category=Docker',
        '--logger',
        'console;verbosity=minimal'
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    $arguments += $DotNetArguments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    Invoke-GmaCompositionDotNet -Arguments $arguments
}
finally {
    $env:GMA_REQUIRE_DOCKER_TESTS = $previousRequireDockerTests
}
