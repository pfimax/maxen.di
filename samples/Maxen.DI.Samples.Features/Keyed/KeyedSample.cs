using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.Keyed;

internal sealed class KeyedSample : ISample
{
    public string Name => "keyed";
    public string Summary => "several registrations of one service, told apart by key and [FromKey]";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<IMessageSender, EmailSender>().WithKey("email");
        builder.Register<IMessageSender, SmsSender>().WithKey("sms");
        builder.Register<Notifier>();

        using var container = builder.Build();

        Console.WriteLine(container.Resolve<IMessageSender>("email").Send("by key"));
        Console.WriteLine(container.Resolve<IMessageSender>("sms").Send("by key"));

        // [FromKey] on a constructor parameter picks the keyed registration without any builder call.
        Console.WriteLine(container.Resolve<Notifier>().Notify("by attribute"));
        return Task.CompletedTask;
    }
}
