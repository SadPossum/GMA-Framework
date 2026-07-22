namespace Gma.Framework.Api.Serilog;

using Microsoft.AspNetCore.Builder;

public static class RequestLoggingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSharedSerilogRequestLogging(this IApplicationBuilder app) =>
        UseGmaSerilogRequestLogging(app);

    public static IApplicationBuilder UseGmaSerilogRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(SafeRequestLoggingMiddleware.InvokeAsync);
    }
}
