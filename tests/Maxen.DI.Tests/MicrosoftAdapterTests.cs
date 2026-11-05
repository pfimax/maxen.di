using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maxen.DI.Tests;

public class MicrosoftAdapterTests
{
    [Fact]
    public void Lifetimes_and_dependencies_carry_over()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, Clock>();
        services.AddScoped<Disposable>();
        services.AddTransient<IGreeter, Greeter>();

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        Assert.Same(provider.GetRequiredService<IClock>(), provider.GetRequiredService<IClock>());
        Assert.NotSame(provider.GetRequiredService<IGreeter>(), provider.GetRequiredService<IGreeter>());

        using var scope = provider.CreateScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<Disposable>(), scope.ServiceProvider.GetRequiredService<Disposable>());
    }

    [Fact]
    public void Factories_instances_and_open_generics_work()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IRepo<>), typeof(Repo<>));
        services.AddSingleton<IClock>(new Clock());
        services.AddTransient<IGreeter>(sp => new Greeter(sp.GetRequiredService<IClock>()));

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        Assert.IsType<Repo<int>>(provider.GetRequiredService<IRepo<int>>());
        Assert.IsType<Greeter>(provider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Keyed_services_are_supported()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IClock, Clock>("a");
        services.AddKeyedSingleton<IClock>("b", (sp, key) => new Clock());

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        Assert.NotNull(provider.GetRequiredKeyedService<IClock>("a"));
        Assert.NotNull(provider.GetRequiredKeyedService<IClock>("b"));
        Assert.Null(provider.GetKeyedService<IClock>("missing"));
    }

    [Fact]
    public void IServiceScopeFactory_creates_working_scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<Disposable>();

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();
        var factory = provider.GetRequiredService<IServiceScopeFactory>();

        Disposable built;
        using (var scope = factory.CreateScope())
        {
            built = scope.ServiceProvider.GetRequiredService<Disposable>();
            Assert.False(built.Disposed);
        }

        Assert.True(built.Disposed);
    }

    [Fact]
    public void Collections_and_missing_services_follow_Microsofts_conventions()
    {
        var services = new ServiceCollection();
        services.AddTransient<IGreeter, QuietGreeter>();
        services.AddTransient<IGreeter, QuietGreeter>();

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        Assert.Equal(2, provider.GetServices<IGreeter>().Count());
        Assert.Null(provider.GetService<IClock>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void IServiceProviderIsService_reports_registrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, Clock>();

        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(IClock)));
        Assert.False(isService.IsService(typeof(IGreeter)));
    }

    [Fact]
    public async Task The_generic_host_runs_on_Maxen()
    {
        using var host = Host.CreateDefaultBuilder()
            .UseMaxenDI(b => b.Decorate<IGreeter, LoudGreeter>())
            .ConfigureServices(s =>
            {
                s.AddSingleton<IClock, Clock>();
                s.AddSingleton<IGreeter, Greeter>();
            })
            .Build();

        await host.StartAsync();

        Assert.Equal("HELLO", host.Services.GetRequiredService<IGreeter>().Greet());
        Assert.IsType<MaxenServiceProvider>(host.Services);

        await host.StopAsync();
    }
}
