using Maxen.DI.Pipeline;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ExtensibilityTests
{
    [Fact]
    public void Property_injection_is_opt_in()
    {
        using var plain = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<WithProperties>();
        });
        Assert.Null(plain.Resolve<WithProperties>().Clock);

        using var enabled = TestContainer.Build(b =>
        {
            b.UsePropertyInjection();
            b.Register<IClock, Clock>();
            b.Register<WithProperties>();
        });
        var instance = enabled.Resolve<WithProperties>();
        Assert.NotNull(instance.Clock);
        Assert.Null(instance.Optional); // optional and unregistered: left alone
    }

    [Fact]
    public void Pre_lifetime_middleware_sees_every_resolve_and_OnActivated_only_creations()
    {
        var log = new List<string>();
        using var container = TestContainer.Build(b =>
        {
            b.Use(MiddlewarePhase.PreLifetime, (ctx, next) =>
            {
                log.Add("resolve " + ctx.Key);
                return next(ctx);
            });
            b.Register<Clock>().AsSingleton().OnActivated((_, _) => log.Add("created"));
        });

        container.Resolve<Clock>();
        container.Resolve<Clock>();

        Assert.Equal(new[] { "resolve Clock", "created", "resolve Clock" }, log);
    }

    [Fact]
    public void A_custom_registration_source_supplies_unregistered_services()
    {
        var builder = new ContainerBuilder();
        builder.AddSource(new AutoMadeSource());
        using var container = builder.Build();

        Assert.Equal("auto", container.Resolve<AutoMade>().Value);
    }
}
