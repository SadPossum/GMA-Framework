namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal static class PersistenceOptionsValidation
{
    public static PersistenceOptions GetValidatedOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        PersistenceOptions options = configuration
            .GetSection(PersistenceOptions.SectionName)
            .Get<PersistenceOptions>() ?? new PersistenceOptions();

        ValidateOptionsResult result = new PersistenceOptionsValidator(configuration).Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(PersistenceOptions.SectionName, typeof(PersistenceOptions), result.Failures);
        }

        return options;
    }
}
