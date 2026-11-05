using Microsoft.Extensions.Hosting;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>A hosted service with a dependency, so the test shows the host builds hosted services through Maxen.DI.</summary>
public sealed class StartupProbe : IHostedService
{
    private readonly ProbeState _state;

    public StartupProbe(IClock clock, ProbeState state) => _state = state;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _state.Started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _state.Stopped = true;
        return Task.CompletedTask;
    }
}
