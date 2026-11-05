using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class BuilderTests
{
    [Fact]
    public void Nonsensical_registrations_are_rejected_immediately()
    {
        var builder = new ContainerBuilder();

        Assert.Throws<RegistrationException>(() => builder.Register(typeof(IClock), typeof(Greeter)));
        Assert.Throws<RegistrationException>(() => builder.Register(typeof(IClock), typeof(IClock)));
    }

    [Fact]
    public void A_builder_can_only_be_used_once()
    {
        var builder = new ContainerBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void TryRegister_leaves_existing_registrations_alone()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IGreeter, QuietGreeter>();
            b.TryRegister<IGreeter, Greeter>();
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
    }
}
