using Maxen.DI.Extensions.AspNetCore;
using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests;

public class AspNetCoreTests
{
    [Fact]
    public async Task The_web_app_runs_on_Maxen_and_requests_get_their_own_scope()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<IClock, Clock>();
        builder.UseMaxenDI();

        await using var app = builder.Build();

        Assert.IsType<MaxenServiceProvider>(app.Services);

        using var first = app.Services.CreateScope();
        using var second = app.Services.CreateScope();

        var a = first.ServiceProvider.GetRequiredService<IClock>();
        Assert.Same(a, first.ServiceProvider.GetRequiredService<IClock>());
        Assert.NotSame(a, second.ServiceProvider.GetRequiredService<IClock>());
    }

    [Fact]
    public void The_Maxen_callback_can_add_decorators()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IClock, Clock>();
        builder.Services.AddSingleton<IGreeter, Greeter>();
        builder.UseMaxenDI(c => c.Decorate<IGreeter, LoudGreeter>());

        using var app = builder.Build();

        Assert.Equal("HELLO", app.Services.GetRequiredService<IGreeter>().Greet());
    }

    [Fact]
    public void A_request_exposes_its_Maxen_scope()
    {
        var builder = WebApplication.CreateBuilder();
        builder.UseMaxenDI();
        using var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        Assert.NotNull(context.GetMaxenScope());
    }

    [Fact]
    public void Without_Maxen_there_is_no_Maxen_scope()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        var context = new DefaultHttpContext { RequestServices = app.Services };

        Assert.Null(context.GetMaxenScope());
    }
}
