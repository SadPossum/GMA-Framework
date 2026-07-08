namespace Gma.Framework.Logging.Serilog;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using global::Serilog;

public static class DependencyInjection
{
    public static IHostBuilder UseConfiguredSerilog(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));
    }

    public static IHostApplicationBuilder AddConfiguredSerilog(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        return builder;
    }
}
