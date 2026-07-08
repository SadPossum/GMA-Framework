namespace Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Naming;

public static class ProjectionRebuildCheckpointEntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<TCheckpointState> ConfigureProjectionRebuildCheckpointState<TCheckpointState>(
        this EntityTypeBuilder<TCheckpointState> builder,
        string tableName = "projection_rebuild_checkpoints")
        where TCheckpointState : ProjectionRebuildCheckpointState
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        builder.ToTable(tableName);
        builder.HasKey(checkpoint => new
        {
            checkpoint.TenantId,
            checkpoint.ProjectionName,
            checkpoint.RunId
        });
        builder.Property(checkpoint => checkpoint.TenantId).HasMaxLength(TenantIds.MaxLength).IsRequired();
        builder.Property(checkpoint => checkpoint.ProjectionName)
            .HasMaxLength(ProjectionRebuildCheckpointState.ProjectionNameMaxLength)
            .IsRequired();
        builder.Property(checkpoint => checkpoint.Cursor)
            .HasMaxLength(ProjectionRebuildCheckpointState.CursorMaxLength);
        builder.Property(checkpoint => checkpoint.ProjectionVersion).IsRequired();
        builder.Property(checkpoint => checkpoint.ProcessedCount).IsRequired();
        builder.Property(checkpoint => checkpoint.WrittenCount).IsRequired();
        builder.Property(checkpoint => checkpoint.SkippedCount).IsRequired();
        builder.Property(checkpoint => checkpoint.FailedCount).IsRequired();
        builder.Property(checkpoint => checkpoint.UpdatedAtUtc).IsRequired();
        builder.Property(checkpoint => checkpoint.CompletedAtUtc);

        return builder;
    }
}
