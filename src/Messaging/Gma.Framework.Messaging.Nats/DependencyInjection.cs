namespace Gma.Framework.Messaging.Nats;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.ModuleComposition;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddNatsJetStreamMessaging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ValidateNatsOptions(builder.Configuration);
        builder.AddMessagingInfrastructure();
        AddNatsOptionServices(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IEventBus, NatsJetStreamEventBus>());
        builder.ProvideFeature(MessagingCompositionFeatures.EventBusProvided("Gma.Framework.Messaging.Nats"));
        builder.ProvideFeature(MessagingCompositionFeatures.NatsPublishingProvided("Gma.Framework.Messaging.Nats"));
        builder.AddOutboxPublishing();
        return builder;
    }

    public static IHostApplicationBuilder AddNatsJetStreamConsumers(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ValidateNatsOptions(builder.Configuration);
        builder.AddMessagingInfrastructure();
        AddNatsOptionServices(builder);
        builder.Services.TryAddSingleton<IIntegrationEventSubscriptionRegistry, IntegrationEventSubscriptionRegistry>();
        NatsConsumerOptions consumerOptions = builder.Configuration
            .GetSection(NatsConsumerOptions.SectionName)
            .Get<NatsConsumerOptions>() ?? new NatsConsumerOptions();
        if (consumerOptions.Enabled)
        {
            builder.ProvideFeature(MessagingCompositionFeatures.NatsConsumersProvided("Gma.Framework.Messaging.Nats"));
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, NatsJetStreamConsumerService>());
        return builder;
    }

    private static void AddNatsOptionServices(IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<NatsJetStreamOptions>()
            .Bind(builder.Configuration.GetSection(NatsJetStreamOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NatsJetStreamOptions>, NatsJetStreamOptionsValidator>());
        builder.Services
            .AddOptions<NatsConsumerOptions>()
            .Bind(builder.Configuration.GetSection(NatsConsumerOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NatsConsumerOptions>, NatsConsumerOptionsValidator>());
    }

    private static void ValidateNatsOptions(IConfiguration configuration)
    {
        ValidateOptions(
            configuration,
            NatsJetStreamOptions.SectionName,
            new NatsJetStreamOptions(),
            new NatsJetStreamOptionsValidator());
        ValidateOptions(
            configuration,
            NatsConsumerOptions.SectionName,
            new NatsConsumerOptions(),
            new NatsConsumerOptionsValidator());
    }

    private static TOptions ValidateOptions<TOptions>(
        IConfiguration configuration,
        string sectionName,
        TOptions fallbackOptions,
        IValidateOptions<TOptions> validator)
        where TOptions : class
    {
        TOptions options = configuration
            .GetSection(sectionName)
            .Get<TOptions>() ?? fallbackOptions;
        ValidateOptionsResult result = validator.Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(sectionName, typeof(TOptions), result.Failures);
        }

        return options;
    }
}
