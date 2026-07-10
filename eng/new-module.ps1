param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name,

    [switch] $Persistence,
    [switch] $SqlServerMigrations,
    [switch] $PostgreSqlMigrations,
    [switch] $AdminCli,
    [switch] $AdminApi,
    [switch] $Inbox,
    [switch] $Outbox,
    [switch] $Cache,
    [switch] $RegisterInHost,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $CompositionSolution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    throw 'RepositoryRoot is required. Framework-owned tooling must be called by a composition repository wrapper or passed an explicit composition root.'
}

if ([string]::IsNullOrWhiteSpace($CompositionSolution)) {
    throw 'CompositionSolution is required. Framework-owned tooling must not assume a composition repository solution filename.'
}

$script:RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$script:CompositionSolution = $CompositionSolution

function Join-GmaPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return Join-Path $script:RepositoryRoot $Path
}

function Resolve-GmaDotNet {
    $candidates = @()
    $resolutionErrors = @()

    if (-not [string]::IsNullOrWhiteSpace($env:GMA_DOTNET)) {
        $candidates += $env:GMA_DOTNET
    }

    $candidates += 'dotnet'

    foreach ($candidate in $candidates) {
        try {
            Push-Location -LiteralPath $script:RepositoryRoot
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

function Invoke-GmaDotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [string] $WorkingDirectory = $script:RepositoryRoot
    )

    $dotnet = Resolve-GmaDotNet
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $dotnet @Arguments
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function ConvertTo-GmaKebabCase {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    $withAcronymBoundaries = [regex]::Replace($Value, '([A-Z]+)([A-Z][a-z])', '$1-$2')
    $withWordBoundaries = [regex]::Replace($withAcronymBoundaries, '([a-z0-9])([A-Z])', '$1-$2')
    return $withWordBoundaries.ToLowerInvariant()
}

if ($SqlServerMigrations -or $PostgreSqlMigrations -or $Inbox -or $Outbox) {
    $Persistence = $true
}

$moduleRoot = Join-GmaPath "src\Modules\$Name"
$moduleName = ConvertTo-GmaKebabCase -Value $Name

if (Test-Path -LiteralPath $moduleRoot) {
    throw "Module '$Name' already exists at '$moduleRoot'."
}

function Write-GmaFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
}

function Add-GmaProject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath
    )

    Invoke-GmaDotNet -Arguments @(
        'sln',
        (Join-GmaPath $script:CompositionSolution),
        'add',
        $ProjectPath,
        '--solution-folder',
        "src/Modules/$Name"
    )
}

New-Item -ItemType Directory -Force -Path $moduleRoot | Out-Null

$contractsProject = Join-Path $moduleRoot "$Name.Contracts\$Name.Contracts.csproj"
$domainProject = Join-Path $moduleRoot "$Name.Domain\$Name.Domain.csproj"
$applicationProject = Join-Path $moduleRoot "$Name.Application\$Name.Application.csproj"
$apiProject = Join-Path $moduleRoot "$Name.Api\$Name.Api.csproj"
$persistenceProject = Join-Path $moduleRoot "$Name.Persistence\$Name.Persistence.csproj"
$adminContractsProject = Join-Path $moduleRoot "$Name.Admin.Contracts\$Name.Admin.Contracts.csproj"
$adminCliProject = Join-Path $moduleRoot "$Name.AdminCli\$Name.AdminCli.csproj"
$adminApiProject = Join-Path $moduleRoot "$Name.AdminApi\$Name.AdminApi.csproj"
$metadataSchemaLine = if ($Persistence) {
    "    public const string Schema = `"$moduleName`";"
}
else {
    "    public static string? Schema => null;"
}
$metadataCacheLines = if ($Cache) {
    @(
        "    public const string ModuleCacheTag = `"$moduleName.module`";",
        '    public const string ModuleCacheEntry = "module";'
    )
}
else {
    @()
}
$metadataPermissionDescriptor = if ($AdminCli -or $AdminApi) {
    "new ModulePermissionDescriptor(${Name}AdminPermissionCodes.Manage, `"Manage $Name administration operations.`", scopeRequirement: PermissionScopeRequirement.Scoped)"
}
else {
    $null
}
$metadataDescriptorLines = @(
    'ModuleDescriptor',
    '        .Create(Name)',
    '        .WithSchema(Schema)'
)
if ($metadataPermissionDescriptor) {
    $metadataDescriptorLines += "        .WithPermission($metadataPermissionDescriptor)"
}
if ($Cache) {
    $metadataDescriptorLines += '        .WithCacheEntry(new ModuleCacheDescriptor(ModuleCacheEntry, CacheScope.Scope, [ModuleCacheTag]))'
}
$metadataDescriptorLines += '        .Build()'
$metadataDescriptor = $metadataDescriptorLines -join "`r`n"
$metadataUsings = @("using Gma.Framework.Modules;")
if ($AdminCli -or $AdminApi) {
    $metadataUsings = @("using Gma.Framework.Permissions;") + $metadataUsings
}
if ($Cache) {
    $metadataUsings = @("using Gma.Framework.Caching;") + $metadataUsings
}
$contractsProjectReferences = @(
    '    <ProjectReference Include="$(GmaFrameworkRoot)Modules\Gma.Framework.Modules\Gma.Framework.Modules.csproj" />'
)
if ($AdminCli -or $AdminApi) {
    $contractsProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Security\Gma.Framework.Permissions\Gma.Framework.Permissions.csproj" />'
}
if ($Cache) {
    $contractsProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Caching\Gma.Framework.Caching\Gma.Framework.Caching.csproj" />'
}

Write-GmaFile $contractsProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
$($contractsProjectReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

Write-GmaFile (Join-Path $moduleRoot "$Name.Contracts\Metadata\${Name}ModuleMetadata.cs") @"
namespace $Name.Contracts;

$($metadataUsings -join "`r`n")

public static class ${Name}ModuleMetadata
{
    public const string Name = "$moduleName";
$metadataSchemaLine
$($metadataCacheLines -join "`r`n")

    public static ModuleDescriptor Descriptor { get; } = $metadataDescriptor;
}
"@

if ($AdminCli -or $AdminApi) {
    Write-GmaFile (Join-Path $moduleRoot "$Name.Contracts\Metadata\${Name}AdminPermissionCodes.cs") @"
namespace $Name.Contracts;

public static class ${Name}AdminPermissionCodes
{
    public const string Manage = ${Name}ModuleMetadata.Name + ".manage";
}
"@
}

Write-GmaFile $domainProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="`$(GmaFrameworkRoot)Domain\Gma.Framework.Domain\Gma.Framework.Domain.csproj" />
    <ProjectReference Include="`$(GmaFrameworkRoot)Results\Gma.Framework.Results\Gma.Framework.Results.csproj" />
  </ItemGroup>
</Project>
"@

$applicationReferences = @(
    "    <ProjectReference Include=`"..\$Name.Contracts\$Name.Contracts.csproj`" />",
    "    <ProjectReference Include=`"..\$Name.Domain\$Name.Domain.csproj`" />",
    '    <ProjectReference Include="$(GmaFrameworkRoot)Application\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Application\Gma.Framework.Application.Composition\Gma.Framework.Application.Composition.csproj" />',
    '    <ProjectReference Include="$(GmaFrameworkRoot)Results\Gma.Framework.Results\Gma.Framework.Results.csproj" />'
)
$applicationUsings = @(
    'using Microsoft.Extensions.DependencyInjection;',
    'using Gma.Framework.Application.Composition;'
)

if ($Cache) {
    $applicationReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Caching\Gma.Framework.Caching\Gma.Framework.Caching.csproj" />'
    $applicationUsings += 'using Gma.Framework.Caching;'
}

Write-GmaFile $applicationProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
$($applicationReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

Write-GmaFile (Join-Path $moduleRoot "$Name.Application\DependencyInjection.cs") @"
namespace $Name.Application;

$($applicationUsings | Sort-Object | Get-Unique | Out-String)public static class DependencyInjection
{
    public static IServiceCollection Add${Name}Application(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
"@

if ($Cache) {
    Write-GmaFile (Join-Path $moduleRoot "$Name.Application\${Name}Cache.cs") @"
namespace $Name.Application;

using $Name.Contracts;
using Gma.Framework.Caching;

internal static class ${Name}Cache
{
    public static CacheKey ModuleKey(params string[] segments) => CacheKey.Scoped(
        ${Name}ModuleMetadata.Name,
        ${Name}ModuleMetadata.ModuleCacheEntry,
        segments);

    public static CacheTag ModuleTag() => CacheTag.Scoped(
        ${Name}ModuleMetadata.Name,
        ${Name}ModuleMetadata.ModuleCacheTag);
}
"@
}

$apiReferences = @(
    "    <ProjectReference Include=`"..\$Name.Application\$Name.Application.csproj`" />",
    "    <ProjectReference Include=`"..\$Name.Contracts\$Name.Contracts.csproj`" />",
    '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />'
)

$apiUsings = @(
    "using $Name.Application;",
    "using $Name.Contracts;",
    'using Microsoft.AspNetCore.Builder;',
    'using Microsoft.AspNetCore.Http;',
    'using Microsoft.AspNetCore.Routing;',
    'using Microsoft.Extensions.DependencyInjection;',
    'using Microsoft.Extensions.Hosting;',
    'using Gma.Framework.Api.Modules;',
    'using Gma.Framework.Api.Observability;'
)

$apiServices = @("        builder.Services.Add${Name}Application();")

if ($Persistence) {
    $apiReferences += "    <ProjectReference Include=`"..\$Name.Persistence\$Name.Persistence.csproj`" />"
    $apiUsings += "using $Name.Persistence;"
    $apiServices += "        builder.Add${Name}Persistence();"
}

Write-GmaFile $apiProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
$($apiReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

Write-GmaFile (Join-Path $moduleRoot "$Name.Api\${Name}Module.cs") @"
namespace $Name.Api;

$($apiUsings | Sort-Object | Get-Unique | Out-String)public sealed class ${Name}Module : IModule
{
    public string Name => ${Name}ModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
$($apiServices -join "`r`n")
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/" + ${Name}ModuleMetadata.Name)
            .WithModuleName(this.Name)
            .WithTags("$Name");

        group.MapGet("/health", Results.NoContent);
    }
}
"@

if ($Persistence) {
    $dbSets = @()
    if ($Outbox) {
        $dbSets += '    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();'
    }
    if ($Inbox) {
        $dbSets += '    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();'
    }

    $dbContextUsings = @(
        'using Microsoft.EntityFrameworkCore;',
        'using Gma.Framework.Persistence.EntityFrameworkCore;',
        'using Gma.Framework.Scoping;'
    )
    if ($Outbox -or $Inbox) {
        $dbContextUsings += 'using Gma.Framework.Messaging.Infrastructure;'
    }

    $persistenceProjectReferences = @(
        "    <ProjectReference Include=`"..\$Name.Contracts\$Name.Contracts.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Application\$Name.Application.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Domain\$Name.Domain.csproj`" />",
        '    <ProjectReference Include="$(GmaFrameworkRoot)Application\Gma.Framework.Application.Events\Gma.Framework.Application.Events.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Domain\Gma.Framework.Domain\Gma.Framework.Domain.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Persistence\Gma.Framework.Persistence.EntityFrameworkCore\Gma.Framework.Persistence.EntityFrameworkCore.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Scoping\Gma.Framework.Scoping\Gma.Framework.Scoping.csproj" />'
    )
    if ($Outbox -or $Inbox) {
        $persistenceProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Naming\Gma.Framework.Naming\Gma.Framework.Naming.csproj" />'
        $persistenceProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Messaging\Gma.Framework.Messaging\Gma.Framework.Messaging.csproj" />'
        $persistenceProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Messaging\Gma.Framework.Messaging.Infrastructure\Gma.Framework.Messaging.Infrastructure.csproj" />'
        $persistenceProjectReferences += '    <ProjectReference Include="$(GmaFrameworkRoot)Runtime\Gma.Framework.Runtime\Gma.Framework.Runtime.csproj" />'
    }

    Write-GmaFile $persistenceProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
$($persistenceProjectReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}DbContext.cs") @"
namespace $Name.Persistence;

$($dbContextUsings | Sort-Object | Get-Unique | Out-String)public sealed class ${Name}DbContext(DbContextOptions<${Name}DbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<${Name}DbContext>(options, scopeContext)
{
$($dbSets -join "`r`n")

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(${Name}Migrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(${Name}DbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
"@

Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}Migrations.cs") @"
namespace $Name.Persistence;

using $Name.Contracts;

public static class ${Name}Migrations
{
    public const string Schema = ${Name}ModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "$Name.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "$Name.Persistence.PostgreSqlMigrations";
}
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}UnitOfWork.cs") @"
namespace $Name.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class ${Name}UnitOfWork(${Name}DbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<${Name}DbContext>(${Name}Migrations.Schema, dbContext, domainEventDispatcher)
{
}
"@

    $persistenceUsings = @(
        'using Microsoft.EntityFrameworkCore;',
        'using Microsoft.Extensions.DependencyInjection;',
        'using Microsoft.Extensions.DependencyInjection.Extensions;',
        'using Microsoft.Extensions.Hosting;',
        'using Gma.Framework.Cqrs.UnitOfWork;',
        'using Gma.Framework.Persistence.EntityFrameworkCore;'
    )
    $persistenceServices = @(
        "        builder.Services.AddPersistenceOptions(builder.Configuration);",
        '',
        "        builder.Services.TryAddModuleDbContext<${Name}DbContext>(options =>",
        "            options.UseConfiguredProvider(",
        "                builder.Configuration,",
        "                ${Name}Migrations.SqlServerAssembly,",
        "                ${Name}Migrations.PostgreSqlAssembly,",
        "                ${Name}Migrations.Schema,",
        "                ${Name}Migrations.HistoryTable));",
        '',
        "        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, ${Name}UnitOfWork>());"
    )

    if ($Outbox -or $Inbox) {
        $persistenceUsings += 'using Gma.Framework.Messaging;'
    }
    if ($Outbox) {
        $persistenceServices += "        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, ${Name}OutboxWriter>());"
        $persistenceServices += "        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, ${Name}OutboxStore>());"
    }
    if ($Inbox) {
        $persistenceServices += "        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, ${Name}InboxStore>());"
    }

    Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\DependencyInjection.cs") @"
namespace $Name.Persistence;

$($persistenceUsings | Sort-Object | Get-Unique | Out-String)public static class DependencyInjection
{
    public static IHostApplicationBuilder Add${Name}Persistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

$($persistenceServices -join "`r`n")

        return builder;
    }
}
"@

    if ($Outbox) {
        Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}OutboxWriter.cs") @"
namespace $Name.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class ${Name}OutboxWriter(
    ${Name}DbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<${Name}DbContext>(dbContext, clock, applicationIdentity, ${Name}Migrations.Schema);
"@

        Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}OutboxStore.cs") @"
namespace $Name.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class ${Name}OutboxStore(${Name}DbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<${Name}DbContext>(dbContext, options, ${Name}Migrations.Schema);
"@

        Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\Configurations\OutboxMessageConfiguration.cs") @"
namespace $Name.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        => builder.ConfigureOutboxMessage();
}
"@
    }

    if ($Inbox) {
        Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\${Name}InboxStore.cs") @"
namespace $Name.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class ${Name}InboxStore(${Name}DbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<${Name}DbContext>(dbContext, clock, idGenerator, ${Name}Migrations.Schema);
"@

        Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence\Configurations\InboxMessageConfiguration.cs") @"
namespace $Name.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
        => builder.ConfigureInboxMessage();
}
"@
    }
}

if ($SqlServerMigrations) {
    $project = Join-Path $moduleRoot "$Name.Persistence.SqlServerMigrations\$Name.Persistence.SqlServerMigrations.csproj"
    Write-GmaFile $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <NoWarn>`$(NoWarn);CA1861;IDE0065;IDE0161;IDE0300</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\$Name.Persistence\$Name.Persistence.csproj" />
  </ItemGroup>
</Project>
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence.SqlServerMigrations\${Name}SqlServerDesignTimeDbContextFactory.cs") @"
namespace $Name.Persistence.SqlServerMigrations;

using $Name.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class ${Name}SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<${Name}DbContext>
{
    public ${Name}DbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<${Name}DbContext>(
                args,
                ${Name}Migrations.SqlServerAssembly,
                ${Name}Migrations.Schema,
                ${Name}Migrations.HistoryTable),
            new DesignTimeScopeContext());
}
"@
}

if ($PostgreSqlMigrations) {
    $project = Join-Path $moduleRoot "$Name.Persistence.PostgreSqlMigrations\$Name.Persistence.PostgreSqlMigrations.csproj"
    Write-GmaFile $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <NoWarn>`$(NoWarn);CA1861;IDE0065;IDE0161;IDE0300</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\$Name.Persistence\$Name.Persistence.csproj" />
  </ItemGroup>
</Project>
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Persistence.PostgreSqlMigrations\${Name}PostgreSqlDesignTimeDbContextFactory.cs") @"
namespace $Name.Persistence.PostgreSqlMigrations;

using $Name.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class ${Name}PostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<${Name}DbContext>
{
    public ${Name}DbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<${Name}DbContext>(
                args,
                ${Name}Migrations.PostgreSqlAssembly,
                ${Name}Migrations.Schema,
                ${Name}Migrations.HistoryTable),
            new DesignTimeScopeContext());
}
"@
}

if ($AdminCli -or $AdminApi) {
Write-GmaFile $adminContractsProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\$Name.Contracts\$Name.Contracts.csproj" />
    <ProjectReference Include="`$(GmaFrameworkRoot)Administration\Gma.Framework.Administration\Gma.Framework.Administration.csproj" />
  </ItemGroup>
</Project>
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Admin.Contracts\Permissions\${Name}AdminPermissions.cs") @"
namespace $Name.Admin.Contracts;

using $Name.Contracts;
using Gma.Framework.Administration;

public static class ${Name}AdminPermissions
{
    public static readonly AdminPermission Manage = AdminPermission.Create(${Name}AdminPermissionCodes.Manage);
}
"@

    Write-GmaFile (Join-Path $moduleRoot "$Name.Admin.Contracts\Operations\${Name}AdminOperationNames.cs") @"
namespace $Name.Admin.Contracts;

public static class ${Name}AdminOperationNames
{
}
"@
}

if ($AdminCli) {
    $adminCliReferences = @(
        "    <ProjectReference Include=`"..\$Name.Admin.Contracts\$Name.Admin.Contracts.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Application\$Name.Application.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Contracts\$Name.Contracts.csproj`" />",
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration.Cli\Gma.Framework.Administration.Cli.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration\Gma.Framework.Administration.csproj" />'
    )
    $adminCliServices = @("        builder.Services.Add${Name}Application();")

    if ($Persistence) {
        $adminCliReferences += "    <ProjectReference Include=`"..\$Name.Persistence\$Name.Persistence.csproj`" />"
        $adminCliServices += "        builder.Add${Name}Persistence();"
    }

    Write-GmaFile $adminCliProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="System.CommandLine" />
  </ItemGroup>
  <ItemGroup>
$($adminCliReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

    $adminCliUsings = @(
        "using $Name.Application;",
        "using $Name.Contracts;",
        'using Microsoft.Extensions.DependencyInjection;',
        'using Microsoft.Extensions.Hosting;',
        'using Gma.Framework.Administration;',
        'using Gma.Framework.Administration.Cli;',
        'using System.CommandLine;'
    )
    if ($Persistence) {
        $adminCliUsings += "using $Name.Persistence;"
    }

    Write-GmaFile (Join-Path $moduleRoot "$Name.AdminCli\${Name}AdminCliModule.cs") @"
namespace $Name.AdminCli;

$($adminCliUsings | Sort-Object | Get-Unique | Out-String)public sealed class ${Name}AdminCliModule : IAdminCliModule
{
    public string Name => ${Name}ModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
$($adminCliServices -join "`r`n")
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        Command module = new(${Name}ModuleMetadata.Name, "$Name administration operations.");
        commands.AddCommand(this.Name, module);
    }
}
"@
}

if ($AdminApi) {
    $adminApiReferences = @(
        "    <ProjectReference Include=`"..\$Name.Admin.Contracts\$Name.Admin.Contracts.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Application\$Name.Application.csproj`" />",
        "    <ProjectReference Include=`"..\$Name.Contracts\$Name.Contracts.csproj`" />",
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration.Api\Gma.Framework.Administration.Api.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Administration\Gma.Framework.Administration\Gma.Framework.Administration.csproj" />',
        '    <ProjectReference Include="$(GmaFrameworkRoot)Api\Gma.Framework.Api\Gma.Framework.Api.csproj" />'
    )
    $adminApiServices = @("        builder.Services.Add${Name}Application();")

    if ($Persistence) {
        $adminApiReferences += "    <ProjectReference Include=`"..\$Name.Persistence\$Name.Persistence.csproj`" />"
        $adminApiServices += "        builder.Add${Name}Persistence();"
    }

    Write-GmaFile $adminApiProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
$($adminApiReferences -join "`r`n")
  </ItemGroup>
</Project>
"@

    $adminApiUsings = @(
        "using $Name.Application;",
        "using $Name.Contracts;",
        'using Microsoft.AspNetCore.Builder;',
        'using Microsoft.AspNetCore.Http;',
        'using Microsoft.AspNetCore.Routing;',
        'using Microsoft.Extensions.DependencyInjection;',
        'using Microsoft.Extensions.Hosting;',
        'using Gma.Framework.Administration.Api;',
        'using Gma.Framework.Api.Observability;'
    )
    if ($Persistence) {
        $adminApiUsings += "using $Name.Persistence;"
    }

    Write-GmaFile (Join-Path $moduleRoot "$Name.AdminApi\${Name}AdminApiModule.cs") @"
namespace $Name.AdminApi;

$($adminApiUsings | Sort-Object | Get-Unique | Out-String)public sealed class ${Name}AdminApiModule : IAdminApiModule
{
    public string Name => ${Name}ModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
$($adminApiServices -join "`r`n")
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("/api/admin/" + ${Name}ModuleMetadata.Name)
            .WithModuleName(this.Name)
            .WithTags("$Name Admin")
            .RequireAuthorization();
}
"@
}

Add-GmaProject $contractsProject
Add-GmaProject $domainProject
Add-GmaProject $applicationProject
Add-GmaProject $apiProject

if ($Persistence) {
    Add-GmaProject $persistenceProject
}

if ($SqlServerMigrations) {
    Add-GmaProject (Join-Path $moduleRoot "$Name.Persistence.SqlServerMigrations\$Name.Persistence.SqlServerMigrations.csproj")
}

if ($PostgreSqlMigrations) {
    Add-GmaProject (Join-Path $moduleRoot "$Name.Persistence.PostgreSqlMigrations\$Name.Persistence.PostgreSqlMigrations.csproj")
}

if ($AdminCli -or $AdminApi) {
    Add-GmaProject $adminContractsProject
}

if ($AdminCli) {
    Add-GmaProject $adminCliProject
}

if ($AdminApi) {
    Add-GmaProject $adminApiProject
}

if ($RegisterInHost) {
    $hostProject = Join-GmaPath 'src\Hosts\Host.Api\Host.Api.csproj'
    Invoke-GmaDotNet -Arguments @('add', $hostProject, 'reference', $apiProject)

    $programPath = Join-GmaPath 'src\Hosts\Host.Api\Program.cs'
    $program = Get-Content -LiteralPath $programPath -Raw
    $moduleUsing = "using $Name.Api;"
    $moduleRegistration = "builder.AddModule<${Name}Module>();"
    $hostRegistrationAnchor = '// module-scaffold:public-api-modules'

    if (-not $program.Contains($moduleUsing)) {
        $program = "$moduleUsing`r`n$program"
    }

    if (-not $program.Contains($moduleRegistration)) {
        if (-not $program.Contains($hostRegistrationAnchor)) {
            throw "Could not register '$Name' in Host.Api. Expected to find the composition marker '$hostRegistrationAnchor' in '$programPath'. Add '$moduleRegistration' manually."
        }

        $program = $program.Replace($hostRegistrationAnchor, "$moduleRegistration`r`n$hostRegistrationAnchor")
    }

    if (-not $program.Contains($moduleRegistration)) {
        throw "Could not verify '$moduleRegistration' in '$programPath'."
    }

    Set-Content -LiteralPath $programPath -Value $program -Encoding UTF8
}

Write-Host "Created module '$Name' under '$moduleRoot'."
Write-Host 'Next steps:'
Write-Host '1. Add project references, ModuleProjects entries, and the module descriptor to tests\Architecture.Tests\Support\ArchitectureCatalog.cs before running the architecture suite.'
Write-Host '2. Compose the module explicitly in Host.Api, Host.AdminCli, or Host.AdminApi only when that host should own the capability.'
Write-Host '3. Keep public contract/domain-state enums on the repo convention: Unknown = 0, stable persisted numeric values, and explicit validation before business decisions.'
