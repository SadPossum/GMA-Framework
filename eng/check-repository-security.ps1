[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$root = [System.IO.Path]::GetFullPath($RepositoryRoot)

function Assert-ClosedObject {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string[]] $AllowedProperties,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $unknownProperties = @(
        $Value.PSObject.Properties.Name |
            Where-Object { $AllowedProperties -notcontains $_ }
    )
    if ($unknownProperties.Count -gt 0) {
        throw "$Context contains unsupported properties."
    }
}

function Read-JsonDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [int] $MaximumBytes,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    if (-not [System.IO.File]::Exists($Path)) {
        throw "Missing $Context file."
    }

    $fileInfo = [System.IO.FileInfo]::new($Path)
    if ($fileInfo.Length -gt $MaximumBytes) {
        throw "$Context file exceeds its size limit."
    }

    try {
        return [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    }
    catch {
        throw "$Context file is not valid JSON."
    }
}

$requiredFiles = @(
    '.github\dependabot.yml',
    '.github\workflows\security.yml',
    '.gma\repository-security.json',
    '.gma\security-exceptions.json',
    'SECURITY.md'
)
foreach ($relativePath in $requiredFiles) {
    if (-not [System.IO.File]::Exists((Join-Path $root $relativePath))) {
        throw "Missing repository security baseline file '$relativePath'."
    }
}

$manifest = Read-JsonDocument `
    -Path (Join-Path $root '.gma\repository-security.json') `
    -MaximumBytes 16KB `
    -Context 'repository security manifest'
Assert-ClosedObject `
    -Value $manifest `
    -AllowedProperties @(
        'schemaVersion',
        'repository',
        'securityBaseline',
        'dependencyEcosystems'
    ) `
    -Context 'Repository security manifest'
if ($manifest.schemaVersion -ne 1 -or
    $manifest.repository -isnot [string] -or
    $manifest.repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw 'Repository security manifest identity is invalid.'
}

Assert-ClosedObject `
    -Value $manifest.securityBaseline `
    -AllowedProperties @('repository', 'commit') `
    -Context 'Repository security baseline reference'
if ($manifest.securityBaseline.repository -ne 'SadPossum/GMA-Skeleton' -or
    $manifest.securityBaseline.commit -isnot [string] -or
    $manifest.securityBaseline.commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Repository security baseline reference is invalid.'
}

if ($manifest.dependencyEcosystems -isnot [System.Array]) {
    throw 'Repository dependencyEcosystems must be an array.'
}
$dependencyEcosystems = @($manifest.dependencyEcosystems)
$allowedDependencyEcosystems = @(
    'github-actions',
    'gitsubmodule',
    'nuget',
    'npm'
)
if ($dependencyEcosystems.Count -eq 0 -or
    $dependencyEcosystems.Count -ne
        @($dependencyEcosystems | Select-Object -Unique).Count -or
    @($dependencyEcosystems |
        Where-Object { $allowedDependencyEcosystems -notcontains $_ }).Count -gt 0 -or
    $dependencyEcosystems -notcontains 'github-actions') {
    throw 'Repository dependency ecosystems are invalid.'
}

$gitDirectory = Join-Path $root '.git'
if (Test-Path -LiteralPath $gitDirectory) {
    $remoteUrl = & git -C $root remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
        throw 'Repository origin URL is unavailable.'
    }

    $remoteMatch = [regex]::Match(
        $remoteUrl.Trim(),
        '(?i)(?:github\.com(?:-private)?[:/])(?<slug>[^/:\s]+/[^/\s]+?)(?:\.git)?$')
    if (-not $remoteMatch.Success -or
        $remoteMatch.Groups['slug'].Value -ne $manifest.repository) {
        throw 'Repository security manifest does not match the GitHub origin.'
    }
}

$securityWorkflow = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\workflows\security.yml'))
$expectedBaselineReference = "uses: $($manifest.securityBaseline.repository)/.github/actions/security-baseline@$($manifest.securityBaseline.commit)"
foreach ($token in @(
    $expectedBaselineReference,
    'exception-file: .gma/security-exceptions.json',
    'security-events: write',
    'github/codeql-action/upload-sarif@7188fc363630916deb702c7fdcf4e481b751f97a',
    'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
    'retention-days: 30',
    'if-no-files-found: error')) {
    if ($securityWorkflow.IndexOf(
        $token,
        [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security workflow is missing required token '$token'."
    }
}
foreach ($forbiddenToken in @(
    'pull_request_target:',
    'persist-credentials: true')) {
    if ($securityWorkflow.IndexOf(
        $forbiddenToken,
        [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Repository security workflow contains forbidden token '$forbiddenToken'."
    }
}

$dependabot = [System.IO.File]::ReadAllText(
    (Join-Path $root '.github\dependabot.yml'))
foreach ($ecosystem in $dependencyEcosystems) {
    $token = "package-ecosystem: $ecosystem"
    if ($dependabot.IndexOf(
        $token,
        [System.StringComparison]::Ordinal) -lt 0) {
        throw "Dependabot policy is missing ecosystem '$ecosystem'."
    }
}

$securityPolicy = [System.IO.File]::ReadAllText(
    (Join-Path $root 'SECURITY.md'))
$expectedReportingUrl = "https://github.com/$($manifest.repository)/security/advisories/new"
foreach ($token in @(
    'Supported Versions',
    'Report a Vulnerability',
    $expectedReportingUrl)) {
    if ($securityPolicy.IndexOf(
        $token,
        [System.StringComparison]::Ordinal) -lt 0) {
        throw "Repository security policy is missing required token '$token'."
    }
}

$exceptionDocument = Read-JsonDocument `
    -Path (Join-Path $root '.gma\security-exceptions.json') `
    -MaximumBytes 64KB `
    -Context 'security exception'
Assert-ClosedObject `
    -Value $exceptionDocument `
    -AllowedProperties @('schemaVersion', 'exceptions') `
    -Context 'Security exception document'
if ($exceptionDocument.schemaVersion -ne 1 -or
    $exceptionDocument.exceptions -isnot [System.Array]) {
    throw 'Security exception document shape is invalid.'
}

$exceptions = @($exceptionDocument.exceptions)
if ($exceptions.Count -gt 100) {
    throw 'Security exception document exceeds the 100-entry limit.'
}
$today = [datetime]::UtcNow.Date
$latestExpiry = $today.AddDays(90)
for ($index = 0; $index -lt $exceptions.Count; $index++) {
    $exception = $exceptions[$index]
    $context = "Security exception $index"
    Assert-ClosedObject `
        -Value $exception `
        -AllowedProperties @(
            'scanner',
            'findingId',
            'owner',
            'reason',
            'expiresOn',
            'paths',
            'purls'
        ) `
        -Context $context

    foreach ($requiredProperty in @(
        'scanner',
        'findingId',
        'owner',
        'reason',
        'expiresOn')) {
        if ($exception.PSObject.Properties.Name -notcontains $requiredProperty -or
            $exception.$requiredProperty -isnot [string] -or
            [string]::IsNullOrWhiteSpace($exception.$requiredProperty)) {
            throw "$context is missing required bounded metadata."
        }
    }
    if (@('vulnerability', 'misconfiguration', 'secret', 'license') -notcontains
        $exception.scanner) {
        throw "$context scanner is unsupported."
    }
    if ($exception.owner.Length -gt 100 -or
        $exception.reason.Length -lt 10 -or
        $exception.reason.Length -gt 500) {
        throw "$context owner or reason is outside its bounds."
    }

    $expiry = [datetime]::MinValue
    if (-not [datetime]::TryParseExact(
        $exception.expiresOn,
        'yyyy-MM-dd',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::None,
        [ref] $expiry) -or
        $expiry.Date -le $today -or
        $expiry.Date -gt $latestExpiry) {
        throw "$context expiry is invalid."
    }

    $hasPaths = $exception.PSObject.Properties.Name -contains 'paths' -and
        $exception.paths -is [System.Array] -and
        @($exception.paths).Count -gt 0
    $hasPurls = $exception.PSObject.Properties.Name -contains 'purls' -and
        $exception.purls -is [System.Array] -and
        @($exception.purls).Count -gt 0
    if (-not $hasPaths -and -not $hasPurls) {
        throw "$context must be narrowed by paths or purls."
    }
}

$workflowRoot = Join-Path $root '.github'
$workflowFiles = if (Test-Path -LiteralPath $workflowRoot) {
    @(
        Get-ChildItem -LiteralPath $workflowRoot -Recurse -File |
            Where-Object { $_.Extension -in @('.yml', '.yaml') }
    )
}
else {
    @()
}
$usesPattern = [regex] '(?m)^\s*-?\s*uses:\s*([^\s#]+)'
foreach ($file in $workflowFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($match in $usesPattern.Matches($content)) {
        $reference = $match.Groups[1].Value
        if ($reference.StartsWith('./', [System.StringComparison]::Ordinal)) {
            continue
        }

        if ($reference -notmatch '^[^@\s]+@[0-9a-fA-F]{40}$') {
            $relativePath = $file.FullName.Substring(
                $root.TrimEnd('\', '/').Length).TrimStart('\', '/')
            throw "GitHub Action reference '$reference' in '$relativePath' is not pinned to an immutable commit."
        }
    }
}

Write-Host "Repository security baseline is valid for $($manifest.repository)."
