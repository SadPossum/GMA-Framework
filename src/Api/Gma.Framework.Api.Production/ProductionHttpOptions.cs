namespace Gma.Framework.Api.Production;

public sealed class ProductionHttpOptions
{
    public const string SectionName = "Http";

    public bool AllowAnyHost { get; set; }

    public bool HttpsRedirectionEnabled { get; set; } = true;

    public bool HstsEnabled { get; set; } = true;

    public bool SecurityHeadersEnabled { get; set; } = true;

    public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();

    public CorsSettings Cors { get; set; } = new();

    public RequestTimeoutSettings RequestTimeouts { get; set; } = new();

    public RateLimitingSettings RateLimiting { get; set; } = new();

    public PrivateNetworkSettings PrivateNetwork { get; set; } = new();
}

public sealed class PrivateNetworkSettings
{
    public bool Enabled { get; set; }
    public string[] AllowedNetworks { get; set; } = [];
}

public sealed class ForwardedHeadersSettings
{
    public bool Enabled { get; set; }

    public bool AllowUnknownProxies { get; set; }

    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];
}

public sealed class CorsSettings
{
    public bool Enabled { get; set; }

    public bool AllowCredentials { get; set; }

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RequestTimeoutSettings
{
    public bool Enabled { get; set; } = true;

    public int DefaultTimeoutSeconds { get; set; } = 30;
}

public sealed class RateLimitingSettings
{
    public bool Enabled { get; set; } = true;

    public int GlobalPermitLimit { get; set; } = 300;

    public int SensitivePermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public string[] SensitivePathPrefixes { get; set; } =
    [
        "/api/auth/register",
        "/api/auth/login",
        "/api/auth/refresh"
    ];
}
