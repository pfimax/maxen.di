using Maxen.DI.Async;

namespace Maxen.DI.Samples.Features.AsyncInit;

internal sealed class AsyncInitSample : ISample
{
    public string Name => "asyncinit";
    public string Summary => "services that finish setting up asynchronously (ResolveAsync, InitializeSingletonsAsync)";

    public async Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<Connection>().AsSingleton();

        await using var container = builder.Build();

        // Either warm everything at startup...
        await container.InitializeSingletonsAsync();
        Console.WriteLine("after InitializeSingletonsAsync: open = " + container.Resolve<Connection>().IsOpen);

        // ...or await a single service when you need it.
        var again = new ContainerBuilder();
        again.Register<Connection>().AsTransient();
        await using var second = again.Build();
        var connection = await second.ResolveAsync<Connection>();
        Console.WriteLine("after ResolveAsync: open = " + connection.IsOpen);
    }
}
