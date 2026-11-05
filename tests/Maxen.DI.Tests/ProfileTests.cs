using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ProfileTests
{
    [Fact]
    public void A_profile_that_is_not_switched_on_changes_nothing()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IGreeter, Greeter>();
            b.Register<IClock, Clock>();
            b.DefineProfile("Quiet", p => p.Register<IGreeter, QuietGreeter>());
        });

        Assert.IsType<Greeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void An_active_profile_overrides_the_defaults()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IGreeter, Greeter>();
            b.Register<IClock, Clock>();
            b.DefineProfile("Quiet", p => p.Register<IGreeter, QuietGreeter>());
            b.UseProfile("Quiet");
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void Profile_names_ignore_case()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IGreeter, Greeter>();
            b.Register<IClock, Clock>();
            b.DefineProfile("Quiet", p => p.Register<IGreeter, QuietGreeter>());
            b.UseProfile("quiet");
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void Switching_on_an_unknown_profile_fails_and_lists_the_known_ones()
    {
        var builder = new ContainerBuilder();
        builder.DefineProfile("Known", _ => { });
        builder.UseProfile("Missing");

        var error = Assert.Throws<RegistrationException>(() => builder.Build());
        Assert.Contains("Missing", error.Message);
        Assert.Contains("Known", error.Message);
    }

    [Fact]
    public void The_environment_variable_selects_a_profile()
    {
        const string variable = "MAXEN_TEST_PROFILE";
        Environment.SetEnvironmentVariable(variable, "Quiet");
        try
        {
            using var container = TestContainer.Build(b =>
            {
                b.Register<IGreeter, Greeter>();
                b.Register<IClock, Clock>();
                b.DefineProfile("Quiet", p => p.Register<IGreeter, QuietGreeter>());
                b.UseProfileFromEnvironment(variable);
            });

            Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }
}
