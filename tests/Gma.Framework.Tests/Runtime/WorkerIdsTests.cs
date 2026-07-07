namespace Gma.Framework.Tests;

using Gma.Framework.Runtime.Workers;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkerIdsTests
{
    [Fact]
    public void Create_bounds_machine_name_to_mapped_worker_id_length()
    {
        Guid workerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        string value = WorkerIds.Create(new string('x', 512), workerId);

        Assert.Equal(WorkerIds.MaxLength, value.Length);
        Assert.EndsWith($":{workerId:N}", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_empty_worker_guid()
    {
        Assert.Throws<ArgumentException>(() => WorkerIds.Create("node-1", Guid.Empty));
    }

    [Fact]
    public void Create_normalizes_machine_name_whitespace_and_control_characters()
    {
        Guid workerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        string value = WorkerIds.Create($" Node\tA{char.MinValue} ", workerId);

        Assert.Equal($"node-a-:{workerId:N}", value);
    }

    [Fact]
    public void Normalize_rejects_blank_and_overlong_worker_ids()
    {
        Assert.Throws<ArgumentException>(() => WorkerIds.Normalize(" "));
        Assert.Throws<ArgumentException>(() => WorkerIds.Normalize(new string('x', WorkerIds.MaxLength + 1)));
    }

    [Theory]
    [InlineData("worker 1")]
    [InlineData("worker\t1")]
    public void Normalize_rejects_whitespace_and_control_characters(string workerId)
    {
        Assert.Throws<ArgumentException>(() => WorkerIds.Normalize(workerId));
    }
}
