namespace Gma.Framework.Tests;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration.Api;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Api.Modules;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ModuleCompositionTests
{
    [Fact]
    public void Module_extension_entry_points_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ModuleExtensions.AddModule<CountingApiModule>(null!));
        Assert.Throws<ArgumentNullException>(() => ModuleExtensions.MapModules(null!));
        Assert.Throws<ArgumentNullException>(() => AdminApiModuleExtensions.AddAdminApiModule<CountingAdminApiModule>(null!));
        Assert.Throws<ArgumentNullException>(() => AdminApiModuleExtensions.MapAdminApiModules(null!));
        Assert.Throws<ArgumentNullException>(() => AdminCliModuleExtensions.AddAdminModule<CountingAdminCliModule>(null!));
        Assert.Throws<ArgumentNullException>(() => AdminCliModuleExtensions.CreateAdminRootCommand(null!));
        Assert.Throws<ArgumentNullException>(() => AdminCliModuleExtensions.ValidateAdminCliStartup(null!));
    }

    [Fact]
    public void Api_modules_reject_invalid_or_duplicate_names()
    {
        Assert.Same(
            ValidApiModule.Instance,
            Assert.Single(ModuleExtensions.ValidateModules([ValidApiModule.Instance])));

        Assert.Throws<InvalidOperationException>(() =>
            ModuleExtensions.ValidateModules([new ApiModule(" Auth ")]));
        Assert.Throws<InvalidOperationException>(() =>
            ModuleExtensions.ValidateModules([new ApiModule("auth.module")]));

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() =>
            ModuleExtensions.ValidateModules([new ApiModule("auth"), new ApiModule("auth")]));

        Assert.Contains("2 API modules are registered with name 'auth'", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddModule_is_idempotent_for_the_same_module_type()
    {
        CountingApiModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddModule<CountingApiModule>();
        builder.AddModule<CountingApiModule>();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        Assert.Equal(1, CountingApiModule.AddServicesCalls);
        Assert.Single(provider.GetServices<IModule>().OfType<CountingApiModule>());
    }

    [Fact]
    public void AddModule_rejects_duplicate_names_before_second_module_services_run()
    {
        FirstDuplicateNameApiModule.Reset();
        SecondDuplicateNameApiModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddModule<FirstDuplicateNameApiModule>();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddModule<SecondDuplicateNameApiModule>());

        Assert.Contains("module name 'duplicate-name'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, FirstDuplicateNameApiModule.AddServicesCalls);
        Assert.Equal(0, SecondDuplicateNameApiModule.AddServicesCalls);
    }

    [Fact]
    public void Admin_api_modules_reject_invalid_or_duplicate_names()
    {
        Assert.Same(
            ValidAdminApiModule.Instance,
            Assert.Single(AdminApiModuleExtensions.ValidateModules([ValidAdminApiModule.Instance])));

        Assert.Throws<InvalidOperationException>(() =>
            AdminApiModuleExtensions.ValidateModules([new AdminApiModule(" Auth ")]));
        Assert.Throws<InvalidOperationException>(() =>
            AdminApiModuleExtensions.ValidateModules([new AdminApiModule("auth.module")]));

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() =>
            AdminApiModuleExtensions.ValidateModules([new AdminApiModule("auth"), new AdminApiModule("auth")]));

        Assert.Contains("2 admin API modules are registered with name 'auth'", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAdminApiModule_is_idempotent_for_the_same_module_type()
    {
        CountingAdminApiModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddAdminApiModule<CountingAdminApiModule>();
        builder.AddAdminApiModule<CountingAdminApiModule>();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        Assert.Equal(1, CountingAdminApiModule.AddServicesCalls);
        Assert.Single(provider.GetServices<IAdminApiModule>().OfType<CountingAdminApiModule>());
    }

    [Fact]
    public void AddAdminApiModule_rejects_duplicate_names_before_second_module_services_run()
    {
        FirstDuplicateNameAdminApiModule.Reset();
        SecondDuplicateNameAdminApiModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddAdminApiModule<FirstDuplicateNameAdminApiModule>();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdminApiModule<SecondDuplicateNameAdminApiModule>());

        Assert.Contains("module name 'duplicate-name'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, FirstDuplicateNameAdminApiModule.AddServicesCalls);
        Assert.Equal(0, SecondDuplicateNameAdminApiModule.AddServicesCalls);
    }

    [Fact]
    public void Admin_cli_modules_reject_invalid_or_duplicate_names()
    {
        Assert.Same(
            ValidAdminCliModule.Instance,
            Assert.Single(AdminCliModuleExtensions.ValidateModules([ValidAdminCliModule.Instance])));

        Assert.Throws<InvalidOperationException>(() =>
            AdminCliModuleExtensions.ValidateModules([new AdminCliModule(" Auth ")]));
        Assert.Throws<InvalidOperationException>(() =>
            AdminCliModuleExtensions.ValidateModules([new AdminCliModule("auth.module")]));

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() =>
            AdminCliModuleExtensions.ValidateModules([new AdminCliModule("auth"), new AdminCliModule("auth")]));

        Assert.Contains("2 admin CLI modules are registered with name 'auth'", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAdminModule_is_idempotent_for_the_same_module_type()
    {
        CountingAdminCliModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddAdminModule<CountingAdminCliModule>();
        builder.AddAdminModule<CountingAdminCliModule>();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        Assert.Equal(1, CountingAdminCliModule.AddServicesCalls);
        Assert.Single(provider.GetServices<IAdminCliModule>().OfType<CountingAdminCliModule>());
    }

    [Fact]
    public void AddAdminModule_rejects_duplicate_names_before_second_module_services_run()
    {
        FirstDuplicateNameAdminCliModule.Reset();
        SecondDuplicateNameAdminCliModule.Reset();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddAdminModule<FirstDuplicateNameAdminCliModule>();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdminModule<SecondDuplicateNameAdminCliModule>());

        Assert.Contains("module name 'duplicate-name'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, FirstDuplicateNameAdminCliModule.AddServicesCalls);
        Assert.Equal(0, SecondDuplicateNameAdminCliModule.AddServicesCalls);
    }

    [Fact]
    public void ValidateAdminCliStartup_runs_options_startup_validators_without_starting_host()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddOptions<TestStartupOptions>()
            .Configure(options => options.RequiredValue = string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RequiredValue),
                "TestStartupOptions:RequiredValue is required.")
            .ValidateOnStart();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.ValidateAdminCliStartup());

        Assert.Contains("TestStartupOptions:RequiredValue is required.", exception.Failures);
    }

    private sealed class ValidApiModule : IModule
    {
        public static readonly ValidApiModule Instance = new();
        public string Name => "auth";
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class ApiModule(string name) : IModule
    {
        public string Name { get; } = name;
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class TestStartupOptions
    {
        public string RequiredValue { get; set; } = string.Empty;
    }

    private sealed class CountingApiModule : IModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "counting";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class FirstDuplicateNameApiModule : IModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class SecondDuplicateNameApiModule : IModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class ValidAdminApiModule : IAdminApiModule
    {
        public static readonly ValidAdminApiModule Instance = new();
        public string Name => "auth";
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class AdminApiModule(string name) : IAdminApiModule
    {
        public string Name { get; } = name;
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class CountingAdminApiModule : IAdminApiModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "counting";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class FirstDuplicateNameAdminApiModule : IAdminApiModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class SecondDuplicateNameAdminApiModule : IAdminApiModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class ValidAdminCliModule : IAdminCliModule
    {
        public static readonly ValidAdminCliModule Instance = new();
        public string Name => "auth";
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapCommands(IAdminCliCommandRegistry commands)
        {
        }
    }

    private sealed class AdminCliModule(string name) : IAdminCliModule
    {
        public string Name { get; } = name;
        public void AddServices(IHostApplicationBuilder builder)
        {
        }

        public void MapCommands(IAdminCliCommandRegistry commands)
        {
        }
    }

    private sealed class CountingAdminCliModule : IAdminCliModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "counting";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapCommands(IAdminCliCommandRegistry commands)
        {
        }
    }

    private sealed class FirstDuplicateNameAdminCliModule : IAdminCliModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapCommands(IAdminCliCommandRegistry commands)
        {
        }
    }

    private sealed class SecondDuplicateNameAdminCliModule : IAdminCliModule
    {
        public static int AddServicesCalls { get; private set; }
        public string Name => "duplicate-name";
        public static void Reset() => AddServicesCalls = 0;
        public void AddServices(IHostApplicationBuilder builder) => AddServicesCalls++;
        public void MapCommands(IAdminCliCommandRegistry commands)
        {
        }
    }
}
