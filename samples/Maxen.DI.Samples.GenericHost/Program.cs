using Maxen.DI;
using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Samples.GenericHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Maxen.DI behind the Generic Host: background services, logging and configuration keep working,
// and Maxen-only features are available through the callback.
using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddHostedService<HeartbeatService>();      // built by Maxen.DI, with its dependencies
    })
    .UseMaxenDI(container =>
    {
        container.Decorate<IClock, FixedClock>();            // Maxen-only: decorate a Microsoft-registered service
    })
    .Build();

await host.StartAsync();
await Task.Delay(150);
await host.StopAsync();

// The same adapter works without a host: build a provider straight from a ServiceCollection.
var services = new ServiceCollection();
services.AddSingleton<IClock, SystemClock>();
await using (var provider = (IAsyncDisposable)services.BuildMaxenServiceProvider())
    Console.WriteLine("standalone provider built and disposed");
