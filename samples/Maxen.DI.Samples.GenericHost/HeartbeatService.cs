using Microsoft.Extensions.Hosting;

namespace Maxen.DI.Samples.GenericHost;

public sealed class HeartbeatService : BackgroundService
{
    private readonly IClock _clock;

    public HeartbeatService(IClock clock) => _clock = clock;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"heartbeat at {_clock.Now:u} (clock is {_clock.GetType().Name})");
        return Task.CompletedTask;
    }
}
