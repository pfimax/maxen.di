using Maxen.DI.Extensibility;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>A module that registers <see cref="Clock"/> with the per-thread lifetime.</summary>
public sealed class PerThreadClockModule : IContainerModule
{
    public void Configure(ContainerBuilder builder)
        => builder.Register<Clock>().WithLifetime(new PerThreadLifetime());
}
