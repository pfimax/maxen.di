using Maxen.DI.Extensibility;

namespace Maxen.DI.Samples.Features.Modules;

/// <summary>A reusable bundle of registrations. Public with a parameterless constructor, so RegisterModules and the maxen-di tool can find it.</summary>
public sealed class ReportingModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
    {
        builder.Register<IReportPrinter, StandardPrinter>().AsSingleton();
    }
}
