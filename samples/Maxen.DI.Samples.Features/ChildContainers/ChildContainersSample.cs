using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.ChildContainers;

internal sealed class ChildContainersSample : ISample
{
    public string Name => "children";
    public string Summary => "a child container inherits its parent and can override per tenant";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<IMessageSender, EmailSender>().AsSingleton();

        using var parent = builder.Build();
        // The tenant overrides one registration; everything else still comes from the parent.
        using var tenant = parent.CreateChild(child => child.Register<IMessageSender, SmsSender>().AsSingleton());

        Console.WriteLine("parent: " + parent.Resolve<IMessageSender>().Send("hello"));
        Console.WriteLine("tenant: " + tenant.Resolve<IMessageSender>().Send("hello"));
        return Task.CompletedTask;
    }
}
