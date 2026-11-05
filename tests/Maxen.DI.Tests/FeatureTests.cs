using Maxen.DI.Extensibility;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class FeatureTests
{
    private sealed class QuietModule : IContainerModule
    {
        public void Configure(ContainerBuilder builder) => builder.Register<IGreeter, QuietGreeter>();
    }

    private static Container Build(Action<ContainerBuilder> configure)
        => TestContainer.Build(b =>
        {
            b.Register<IGreeter, Greeter>();
            b.Register<IClock, Clock>();
            configure(b);
        });

    [Fact]
    public void A_module_behind_a_feature_is_skipped_until_the_feature_is_on()
    {
        using var container = Build(b => b.RegisterModule<QuietModule>("quiet"));

        Assert.IsType<Greeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void Switching_the_feature_on_applies_its_module_over_the_defaults()
    {
        using var container = Build(b =>
        {
            b.RegisterModule<QuietModule>("quiet");
            b.UseFeatures("quiet");
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void Features_can_come_from_an_environment_variable()
    {
        var variable = "MAXEN_TEST_FEATURES_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "other ; quiet");
        try
        {
            using var container = Build(b =>
            {
                b.RegisterModule<QuietModule>("quiet");
                b.DefineProfile("other", _ => { });
                b.UseFeaturesFromEnvironment(variable);
            });

            Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void An_unset_environment_variable_changes_nothing()
    {
        using var container = Build(b =>
        {
            b.RegisterModule<QuietModule>("quiet");
            b.UseFeaturesFromEnvironment("MAXEN_TEST_FEATURES_NOT_SET");
        });

        Assert.IsType<Greeter>(container.Resolve<IGreeter>());
    }
}
