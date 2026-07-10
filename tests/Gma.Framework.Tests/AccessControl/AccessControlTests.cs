namespace Gma.Framework.Tests.AccessControl;

using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AccessControlTests
{
    [Fact]
    public void Access_subject_normalizes_known_kinds_and_ids()
    {
        AccessSubject subject = AccessSubject.User(" user-1 ");

        Assert.Equal(AccessSubjectKind.User, subject.Kind);
        Assert.Equal("user-1", subject.Id);
    }

    [Fact]
    public void Access_subject_factories_cover_admin_service_and_system_callers()
    {
        AccessSubject admin = AccessSubject.AdminActor(" admin-1 ");
        AccessSubject service = AccessSubject.Service(" notifications-worker ");
        AccessSubject system = AccessSubject.System(" platform ");

        Assert.Equal(AccessSubjectKind.AdminActor, admin.Kind);
        Assert.Equal("admin-1", admin.Id);
        Assert.Equal(AccessSubjectKind.Service, service.Kind);
        Assert.Equal("notifications-worker", service.Id);
        Assert.Equal(AccessSubjectKind.System, system.Kind);
        Assert.Equal("platform", system.Id);
    }

    [Fact]
    public void Access_subject_rejects_unknown_kind_and_empty_id()
    {
        Assert.Throws<ArgumentException>(() => new AccessSubject(AccessSubjectKind.Unknown, "user-1"));
        Assert.Throws<ArgumentException>(() => AccessSubject.User(" "));

        Assert.False(AccessSubject.TryCreate(AccessSubjectKind.User, " ", out _));
        Assert.False(AccessSubject.TryCreate((AccessSubjectKind)999, "user-1", out _));
    }

    [Theory]
    [InlineData(" Catalog.Items.Read ", "catalog.items.read")]
    [InlineData("properties.rooms-manage", "properties.rooms-manage")]
    public void Permission_code_normalizes_dot_separated_codes(string input, string expected)
    {
        PermissionCode permission = PermissionCode.Create(input);

        Assert.Equal(expected, permission.Value);
        Assert.Equal(expected, permission.ToString());
        Assert.True(PermissionCode.TryCreate(input, out PermissionCode? parsed));
        Assert.Equal(expected, parsed.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auth")]
    [InlineData("auth..members")]
    [InlineData("Auth Members.Read")]
    [InlineData("*")]
    public void Permission_code_rejects_invalid_codes(string? input)
    {
        Assert.False(PermissionCode.TryCreate(input, out _));

        if (input is not null)
        {
            Assert.Throws<ArgumentException>(() => PermissionCode.Create(input));
        }
    }

    [Fact]
    public void Access_scope_parses_global_tenant_and_resource_segments()
    {
        AccessScope global = AccessScope.Parse(" global ");
        AccessScope tenant = AccessScope.Create(AccessScopeSegment.Create("tenant", "tenant-a"));
        AccessScope resource = AccessScope.Parse("tenant:tenant-a/property:property-1/department:front-desk");

        Assert.True(global.IsGlobal);
        Assert.Equal("global", global.Value);
        Assert.Equal("tenant:tenant-a", tenant.Value);
        Assert.Equal("tenant:tenant-a/property:property-1/department:front-desk", resource.Value);
        Assert.Equal(3, resource.Segments.Count);
    }

    [Fact]
    public void Access_scope_equality_uses_canonical_scope_value()
    {
        AccessScope parsed = AccessScope.Parse("tenant:tenant-a");
        AccessScope factory = AccessScope.Create(AccessScopeSegment.Create("tenant", "tenant-a"));

        Assert.Equal(parsed, factory);
        Assert.Equal(parsed.GetHashCode(), factory.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("/tenant:tenant-a")]
    [InlineData("tenant")]
    [InlineData("tenant:")]
    [InlineData("tenant:tenant a")]
    [InlineData("tenant:tenant-a/")]
    [InlineData("tenant:tenant-a//property:property-1")]
    [InlineData("tenant:tenant-a/property")]
    public void Access_scope_rejects_invalid_scopes(string value)
    {
        Assert.False(AccessScope.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => AccessScope.Parse(value));
    }

    [Fact]
    public void Access_scope_rejects_values_over_the_persisted_length_limit()
    {
        string value = $"tenant:{new string('x', AccessScope.MaxLength)}";

        Assert.False(AccessScope.TryParse(value, out _));
        Assert.Throws<ArgumentException>(() => AccessScope.Parse(value));
    }

    [Fact]
    public void Access_scope_matching_requires_exact_scope_by_default()
    {
        AccessScope tenant = AccessScope.Parse("tenant:tenant-a");
        AccessScope property = AccessScope.Parse("tenant:tenant-a/property:property-1");

        Assert.True(AccessScopeMatcher.GrantSatisfiesRequest(tenant, tenant, new AccessScopeMatchOptions()));
        Assert.True(AccessScopeMatcher.GrantSatisfiesRequest(
            tenant,
            AccessScope.Create(AccessScopeSegment.Create("tenant", "tenant-a")),
            new AccessScopeMatchOptions()));
        Assert.False(AccessScopeMatcher.GrantSatisfiesRequest(tenant, property, new AccessScopeMatchOptions()));
        Assert.False(AccessScopeMatcher.GrantSatisfiesRequest(AccessScope.Global, property, new AccessScopeMatchOptions()));
    }

    [Fact]
    public void Access_scope_matching_supports_explicit_ancestor_and_global_inheritance()
    {
        AccessScope tenant = AccessScope.Parse("tenant:tenant-a");
        AccessScope property = AccessScope.Parse("tenant:tenant-a/property:property-1");
        AccessScope otherTenantProperty = AccessScope.Parse("tenant:tenant-b/property:property-1");

        Assert.True(AccessScopeMatcher.GrantSatisfiesRequest(
            tenant,
            property,
            new AccessScopeMatchOptions(AllowAncestorScopeGrants: true)));
        Assert.False(AccessScopeMatcher.GrantSatisfiesRequest(
            tenant,
            otherTenantProperty,
            new AccessScopeMatchOptions(AllowAncestorScopeGrants: true)));
        Assert.True(AccessScopeMatcher.GrantSatisfiesRequest(
            AccessScope.Global,
            property,
            new AccessScopeMatchOptions(AllowGlobalScopeGrant: true)));
    }

    [Fact]
    public async Task Authorization_service_denies_by_default_when_no_provider_allows()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddGmaAccessControl()
            .BuildServiceProvider();

        IAccessAuthorizationService authorization = provider.GetRequiredService<IAccessAuthorizationService>();
        AccessDecision decision = await authorization.AuthorizeAsync(CreateRequirement(), CancellationToken.None);

        Assert.True(decision.IsDenied);
        Assert.Equal(AccessDecisionReasonCodes.DenyByDefault, decision.ReasonCode);
    }

    [Fact]
    public async Task Authorization_service_returns_allow_when_at_least_one_provider_allows_and_none_deny()
    {
        RecordingProvider abstaining = new(AccessDecision.Abstain());
        RecordingProvider allowing = new(AccessDecision.Allowed());

        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAccessDecisionProvider>(abstaining)
            .AddSingleton<IAccessDecisionProvider>(allowing)
            .AddGmaAccessControl()
            .BuildServiceProvider();

        AccessDecision decision = await provider
            .GetRequiredService<IAccessAuthorizationService>()
            .AuthorizeAsync(CreateRequirement(), CancellationToken.None);

        Assert.True(decision.IsAllowed);
        Assert.Equal(1, abstaining.CallCount);
        Assert.Equal(1, allowing.CallCount);
    }

    [Fact]
    public async Task Authorization_service_denies_by_default_when_all_providers_abstain()
    {
        RecordingProvider abstaining = new(AccessDecision.Abstain());

        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAccessDecisionProvider>(abstaining)
            .AddGmaAccessControl()
            .BuildServiceProvider();

        AccessDecision decision = await provider
            .GetRequiredService<IAccessAuthorizationService>()
            .AuthorizeAsync(CreateRequirement(), CancellationToken.None);

        Assert.True(decision.IsDenied);
        Assert.Equal(AccessDecisionReasonCodes.DenyByDefault, decision.ReasonCode);
        Assert.Equal(1, abstaining.CallCount);
    }

    [Fact]
    public async Task Authorization_service_lets_deny_provider_veto_prior_allows()
    {
        RecordingProvider allowing = new(AccessDecision.Allowed());
        RecordingProvider denying = new(AccessDecision.Denied(AccessDecisionReasonCodes.ProviderDenied, "Blocked."));

        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAccessDecisionProvider>(allowing)
            .AddSingleton<IAccessDecisionProvider>(denying)
            .AddGmaAccessControl()
            .BuildServiceProvider();

        AccessDecision decision = await provider
            .GetRequiredService<IAccessAuthorizationService>()
            .AuthorizeAsync(CreateRequirement(), CancellationToken.None);

        Assert.True(decision.IsDenied);
        Assert.Equal("Blocked.", decision.Message);
        Assert.Equal(1, allowing.CallCount);
        Assert.Equal(1, denying.CallCount);
    }

    [Fact]
    public void Access_decision_normalizes_reason_and_message()
    {
        AccessDecision decision = AccessDecision.Denied(" access.denied ", " Denied by provider. ");

        Assert.True(decision.IsDenied);
        Assert.Equal("access.denied", decision.ReasonCode);
        Assert.Equal("Denied by provider.", decision.Message);
    }

    private static AccessRequirement CreateRequirement() =>
        new(
            AccessSubject.AdminActor("admin-1"),
            PermissionCode.Create("auth.members.read"),
            AccessScope.Create(AccessScopeSegment.Create("tenant", "tenant-a")));

    private sealed class RecordingProvider(AccessDecision decision) : IAccessDecisionProvider
    {
        public int CallCount { get; private set; }

        public Task<AccessDecision> DecideAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(decision);
        }
    }
}
