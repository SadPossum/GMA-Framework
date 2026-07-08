namespace Gma.Framework.Tasks.Cqrs;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Tasks;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTaskCqrs(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddCqrsInfrastructure();
        builder.ProvideFeature(TasksCompositionFeatures.CqrsDispatcherProvided("Gma.Framework.Tasks.Cqrs"));
        builder.Services.TryAddScoped<ITaskCommandDispatcher, TaskCommandDispatcher>();

        return builder;
    }
}
