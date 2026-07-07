namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminOperationExecutionResultTests
{
    [Fact]
    public void Successful_status_requires_successful_result()
    {
        AdminOperationExecutionResult<int> result = new(
            AdminOperationExecutionStatus.Succeeded,
            Result.Success(42),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Result.Value);
    }

    [Fact]
    public void Successful_status_rejects_failed_result()
    {
        Assert.Throws<ArgumentException>(() =>
            new AdminOperationExecutionResult<int>(
                AdminOperationExecutionStatus.Succeeded,
                Result.Failure<int>(AdminErrors.OperationFailed),
                null));
    }

    [Theory]
    [InlineData(AdminOperationExecutionStatus.Failed)]
    [InlineData(AdminOperationExecutionStatus.Unauthorized)]
    [InlineData(AdminOperationExecutionStatus.ValidationFailed)]
    [InlineData(AdminOperationExecutionStatus.UnexpectedFailure)]
    public void Failed_status_rejects_successful_result(AdminOperationExecutionStatus status)
    {
        Assert.Throws<ArgumentException>(() =>
            new AdminOperationExecutionResult<int>(
                status,
                Result.Success(42),
                null));
    }

    [Theory]
    [InlineData(AdminOperationExecutionStatus.Unknown)]
    [InlineData((AdminOperationExecutionStatus)42)]
    public void Status_must_be_known(AdminOperationExecutionStatus status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AdminOperationExecutionResult<int>(
                status,
                Result.Failure<int>(AdminErrors.OperationFailed),
                null));
    }

    [Fact]
    public void Audit_error_is_normalized()
    {
        AdminOperationExecutionResult<int> result = new(
            AdminOperationExecutionStatus.Failed,
            Result.Failure<int>(AdminErrors.OperationFailed),
            " Admin audit failed. ");

        Assert.Equal("Admin audit failed.", result.AuditError);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Audit\u0000failed.")]
    public void Audit_error_must_be_compact_text(string auditError)
    {
        Assert.Throws<ArgumentException>(() =>
            new AdminOperationExecutionResult<int>(
                AdminOperationExecutionStatus.Failed,
                Result.Failure<int>(AdminErrors.OperationFailed),
                auditError));
    }
}
