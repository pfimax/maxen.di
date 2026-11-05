using Maxen.DI.Extensibility;

namespace Maxen.DI.Samples.Features.Modules;

/// <summary>Only applied when the "beta" feature is switched on.</summary>
public sealed class BetaFeatureModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        // Registered after the standard one, so it wins while the feature is on.
        builder.Register<IReportPrinter, BetaPrinter>().AsSingleton();
    }
}
