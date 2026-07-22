namespace Gma.Framework.Api.Production;

using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    internal const string CorsPolicyName = "gma-configured-origins";

    public static IHostApplicationBuilder AddGmaProductionHttp(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ProductionHttpRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection section = builder.Configuration.GetSection(ProductionHttpOptions.SectionName);
        ProductionHttpOptions options = section.Get<ProductionHttpOptions>() ?? new();
        ValidateBeforeRegistration(options, builder.Environment, builder.Configuration);

        builder.Services.AddSingleton<ProductionHttpRegistrationMarker>();
        builder.Services
            .AddOptions<ProductionHttpOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ProductionHttpOptions>, ProductionHttpOptionsValidator>());

        builder.Services.AddProblemDetails(problemDetails =>
        {
            problemDetails.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });
        builder.Services.AddExceptionHandler<OptimisticConcurrencyExceptionHandler>();
        builder.Services.AddExceptionHandler<SanitizedUnhandledExceptionHandler>();
        builder.Services.AddHealthChecks();

        ConfigureForwardedHeaders(builder.Services, options.ForwardedHeaders);
        ConfigureCors(builder.Services, options.Cors);
        ConfigureRequestTimeouts(builder.Services, options.RequestTimeouts);
        ConfigureRateLimiting(builder.Services, options.RateLimiting);

        return builder;
    }

    public static WebApplication UseGmaProductionHttp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ProductionHttpOptions options = app.Services.GetRequiredService<IOptions<ProductionHttpOptions>>().Value;

        if (options.ForwardedHeaders.Enabled)
        {
            app.UseForwardedHeaders();
        }

        app.UseExceptionHandler();

        if (options.PrivateNetwork.Enabled)
        {
            System.Net.IPNetwork[] allowedNetworks =
                [.. options.PrivateNetwork.AllowedNetworks.Select(System.Net.IPNetwork.Parse)];
            app.Use(async (context, next) =>
            {
                IPAddress? address = context.Connection.RemoteIpAddress;
                if (address is null ||
                    (!IsPrivateAddress(address) && !allowedNetworks.Any(network => network.Contains(address))))
                {
                    await Results.Problem(
                            title: "Http.PrivateNetworkRequired",
                            detail: "This endpoint is available only through an approved private network boundary.",
                            statusCode: StatusCodes.Status403Forbidden)
                        .ExecuteAsync(context)
                        .ConfigureAwait(false);
                    return;
                }

                await next(context).ConfigureAwait(false);
            });
        }

        if (!app.Environment.IsDevelopment() && options.HstsEnabled)
        {
            app.UseHsts();
        }

        if (options.HttpsRedirectionEnabled)
        {
            app.UseHttpsRedirection();
        }

        if (!app.Environment.IsDevelopment() && options.SecurityHeadersEnabled)
        {
            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
                    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
                    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
                    context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
                    return Task.CompletedTask;
                });

                await next(context).ConfigureAwait(false);
            });
        }

        if (options.Cors.Enabled)
        {
            app.UseCors(CorsPolicyName);
        }

        if (options.RequestTimeouts.Enabled)
        {
            app.UseRequestTimeouts();
        }

        if (options.RateLimiting.Enabled)
        {
            app.UseRateLimiter();
        }

        return app;
    }

    private static void ConfigureForwardedHeaders(
        IServiceCollection services,
        ForwardedHeadersSettings options)
    {
        if (!options.Enabled)
        {
            return;
        }

        services.Configure<ForwardedHeadersOptions>(configured =>
        {
            configured.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                          ForwardedHeaders.XForwardedProto |
                                          ForwardedHeaders.XForwardedHost;
            configured.ForwardLimit = options.ForwardLimit;

            if (options.AllowUnknownProxies)
            {
                configured.KnownIPNetworks.Clear();
                configured.KnownProxies.Clear();
            }
            else
            {
                foreach (string knownProxy in options.KnownProxies)
                {
                    configured.KnownProxies.Add(IPAddress.Parse(knownProxy));
                }
            }
        });
    }

    private static void ConfigureCors(IServiceCollection services, CorsSettings options)
    {
        services.AddCors(configured =>
        {
            configured.AddPolicy(CorsPolicyName, policy =>
            {
                if (!options.Enabled)
                {
                    policy.SetIsOriginAllowed(_ => false);
                    return;
                }

                policy.WithOrigins(options.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                if (options.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
        });
    }

    private static void ConfigureRequestTimeouts(
        IServiceCollection services,
        RequestTimeoutSettings options)
    {
        services.AddRequestTimeouts(configured =>
        {
            configured.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(options.DefaultTimeoutSeconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
            };
        });
    }

    private static void ConfigureRateLimiting(
        IServiceCollection services,
        RateLimitingSettings options)
    {
        services.AddRateLimiter(configured =>
        {
            configured.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            configured.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = options.WindowSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                await Results.Problem(
                        title: "Http.RateLimitExceeded",
                        detail: "Too many requests. Retry after the indicated interval.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext)
                    .ConfigureAwait(false);
            };

            if (!options.Enabled)
            {
                return;
            }

            configured.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                bool sensitive = options.SensitivePathPrefixes.Any(prefix =>
                    context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
                string client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                string partitionKey = $"{(sensitive ? "sensitive" : "global")}:{client}";
                int permitLimit = sensitive ? options.SensitivePermitLimit : options.GlobalPermitLimit;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds)
                    });
            });
        });
    }

    private static void ValidateBeforeRegistration(
        ProductionHttpOptions options,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            environment.IsDevelopment(),
            configuration["AllowedHosts"]);
        if (failures.Length > 0)
        {
            throw new OptionsValidationException(
                ProductionHttpOptions.SectionName,
                typeof(ProductionHttpOptions),
                failures);
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254);
    }

    private sealed class ProductionHttpRegistrationMarker;
}
