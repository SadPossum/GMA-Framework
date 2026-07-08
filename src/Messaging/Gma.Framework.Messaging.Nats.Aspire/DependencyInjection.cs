namespace Gma.Framework.Messaging.Nats.Aspire;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Nats;

public static class DependencyInjection
{
    private const string ConnectionName = "nats";

    public static IHostApplicationBuilder AddConfiguredNatsJetStreamMessaging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection section = builder.Configuration.GetSection(NatsJetStreamOptions.SectionName);
        NatsJetStreamOptions natsOptions = section.Get<NatsJetStreamOptions>() ?? new NatsJetStreamOptions();

        if (!natsOptions.Enabled)
        {
            return builder;
        }

        ValidateNatsOptions(natsOptions);
        AddConfiguredNatsClient(
            builder,
            NatsJetStreamOptions.SectionName,
            typeof(NatsJetStreamOptions),
            "NATS JetStream publishing is enabled");
        builder.AddNatsJetStreamMessaging();

        return builder;
    }

    public static IHostApplicationBuilder AddConfiguredNatsJetStreamConsumers(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection section = builder.Configuration.GetSection(NatsConsumerOptions.SectionName);
        NatsConsumerOptions consumerOptions = section.Get<NatsConsumerOptions>() ?? new NatsConsumerOptions();

        if (!consumerOptions.Enabled)
        {
            return builder;
        }

        AddConfiguredNatsClient(
            builder,
            NatsConsumerOptions.SectionName,
            typeof(NatsConsumerOptions),
            "NATS JetStream consumers are enabled");
        builder.AddNatsJetStreamConsumers();

        return builder;
    }

    private static void AddConfiguredNatsClient(
        IHostApplicationBuilder builder,
        string optionsSectionName,
        Type optionsType,
        string reason)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ConfiguredNatsClientRegistrationMarker)))
        {
            return;
        }

        string? connectionString = builder.Configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new OptionsValidationException(
                optionsSectionName,
                optionsType,
                [$"ConnectionStrings:{ConnectionName} is required when {reason}."]);
        }

        builder.AddNatsClient(ConnectionName, (_, options) =>
        {
            return options with { Url = connectionString };
        });
        builder.Services.AddSingleton<ConfiguredNatsClientRegistrationMarker>();
    }

    private static void ValidateNatsOptions(NatsJetStreamOptions options)
    {
        ValidateOptionsResult result = new NatsJetStreamOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(NatsJetStreamOptions.SectionName, typeof(NatsJetStreamOptions), result.Failures);
        }
    }

    private sealed class ConfiguredNatsClientRegistrationMarker;
}
