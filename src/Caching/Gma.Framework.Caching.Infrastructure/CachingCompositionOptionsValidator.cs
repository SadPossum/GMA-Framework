namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Caching;

internal sealed class CachingCompositionOptionsValidator(IServiceProvider serviceProvider)
    : IValidateOptions<CachingOptions>
{
    public ValidateOptionsResult Validate(string? name, CachingOptions options)
    {
        try
        {
            _ = CachingCompositionGuard.EnsureValid(options, serviceProvider);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }
}
