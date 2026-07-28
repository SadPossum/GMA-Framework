namespace Gma.Framework.Api.Production;

using System.Globalization;
using Gma.Framework.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

internal sealed class DistributedHttpRateLimitMiddleware(
    RequestDelegate next,
    IMultiPartitionRateLimiter rateLimiter,
    IOptions<ProductionHttpOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        RateLimitingSettings settings = options.Value.RateLimiting;
        MultiPartitionRateLimitRequest request =
            HttpRateLimitPolicy.CreateDistributedRequest(context, settings);
        MultiPartitionRateLimitDecision decision = await rateLimiter
            .AcquireAsync(request, context.RequestAborted)
            .ConfigureAwait(false);

        switch (decision.Outcome)
        {
            case MultiPartitionRateLimitOutcome.Acquired:
                await next(context).ConfigureAwait(false);
                return;

            case MultiPartitionRateLimitOutcome.Rejected
                when decision.RetryAfter.HasValue:
                context.Response.Headers.RetryAfter = Math.Max(
                        1,
                        (int)Math.Ceiling(decision.RetryAfter.Value.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
                await Results.Problem(
                        title: "Http.RateLimitExceeded",
                        detail: "Too many requests. Retry after the indicated interval.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context)
                    .ConfigureAwait(false);
                return;

            case MultiPartitionRateLimitOutcome.Rejected:
            case MultiPartitionRateLimitOutcome.ProviderUnavailable:
            case MultiPartitionRateLimitOutcome.Unknown:
            default:
                context.Response.Headers.RetryAfter = "1";
                await Results.Problem(
                        title: "Http.RateLimitProviderUnavailable",
                        detail: "Request admission is temporarily unavailable.",
                        statusCode: StatusCodes.Status503ServiceUnavailable)
                    .ExecuteAsync(context)
                    .ConfigureAwait(false);
                return;
        }
    }
}
