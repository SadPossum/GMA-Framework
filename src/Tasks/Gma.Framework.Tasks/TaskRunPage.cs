namespace Gma.Framework.Tasks;

public sealed record TaskRunPage(
    IReadOnlyList<TaskRunSummary> Items,
    int TotalCount,
    int Page,
    int PageSize);
