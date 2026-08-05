namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class RelationalStringPropertyBuilderExtensions
{
    internal const string SqlServerOrdinalCollation = "Latin1_General_100_BIN2";

    public static PropertyBuilder<TProperty> UseOrdinalStringComparison<TProperty>(
        this PropertyBuilder<TProperty> builder,
        DbContext context)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        if (typeof(TProperty) != typeof(string))
        {
            throw new InvalidOperationException(
                "Ordinal string comparison can only be configured for string properties.");
        }

        if (context.Database.IsSqlServer())
        {
            builder.UseCollation(SqlServerOrdinalCollation);
        }

        return builder;
    }
}
