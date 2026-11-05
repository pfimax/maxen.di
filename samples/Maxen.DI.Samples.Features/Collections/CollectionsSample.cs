using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.Collections;

internal sealed class CollectionsSample : ISample
{
    public string Name => "collections";
    public string Summary => "ask for every registration with ResolveAll or IEnumerable<T>";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<IMessageSender, EmailSender>();
        builder.Register<IMessageSender, SmsSender>();
        builder.Register<Broadcaster>();

        using var container = builder.Build();

        Console.WriteLine("ResolveAll: " + string.Join(", ", container.ResolveAll<IMessageSender>().Select(s => s.GetType().Name)));
        Console.WriteLine("injected:   " + string.Join(" | ", container.Resolve<Broadcaster>().SendToAll("news")));
        return Task.CompletedTask;
    }
}
