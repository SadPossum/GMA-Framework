namespace Gma.Framework.Api.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerGen;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddSharedOpenApi(this IHostApplicationBuilder builder) =>
        AddGmaOpenApi(builder);

    public static IHostApplicationBuilder AddGmaOpenApi(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.Replace(ServiceDescriptor.Transient<
            ISerializerDataContractResolver,
            GmaOpenApiDataContractResolver>());

        return builder;
    }

    public static WebApplication UseSharedOpenApi(this WebApplication app) =>
        UseGmaOpenApi(app);

    public static WebApplication UseGmaOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
