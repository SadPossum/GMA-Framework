namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminActorTests
{
    [Fact]
    public void System_normalizes_actor_id()
    {
        AdminActor actor = AdminActor.System(" Actor-1 ");

        Assert.Equal("Actor-1", actor.Id);
    }

    [Fact]
    public void System_rejects_blank_actor_id()
    {
        Assert.Throws<ArgumentException>(() => AdminActor.System(" "));
    }

    [Fact]
    public void System_rejects_ambiguous_actor_id_characters()
    {
        Assert.Throws<ArgumentException>(() => AdminActor.System("actor 1"));
        Assert.Throws<ArgumentException>(() => AdminActor.System($"actor{char.MinValue}1"));
    }

    [Fact]
    public void System_rejects_overlong_actor_id()
    {
        Assert.Throws<ArgumentException>(() => AdminActor.System(new string('x', AdminActor.MaxLength + 1)));
    }

    [Fact]
    public void Try_system_returns_false_for_invalid_actor_id()
    {
        Assert.False(AdminActor.TrySystem("actor 1", out AdminActor? actor));

        Assert.Null(actor);
    }

    [Fact]
    public void Invalid_actor_message_mentions_current_max_length()
    {
        Assert.Contains(AdminActor.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), AdminActor.InvalidIdMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_actor_cannot_be_constructed_without_factory()
    {
        Assert.DoesNotContain(
            typeof(AdminActor).GetConstructors(),
            constructor => constructor.IsPublic);
    }
}
