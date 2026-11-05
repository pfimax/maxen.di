namespace Maxen.DI.Samples.Features.Registration;

internal sealed class RegistrationSample : ISample
{
    public string Name => "registration";
    public string Summary => "type, instance, factory, TryRegister, Replace, AlsoAs and ownership";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();

        builder.Register<IMessageSender, EmailSender>().AsSingleton();

        // TryRegister only adds the registration when nothing is there yet.
        var added = builder.TryRegister<IMessageSender, SmsSender>();
        Console.WriteLine($"TryRegister added a second sender: {added is not null}");

        // An existing object, and a factory that runs when someone asks.
        builder.RegisterInstance("a plain string instance");
        builder.RegisterFactory<DateTime>(_ => DateTime.UtcNow).AsTransient();

        // One implementation answering to several service types.
        builder.Register<SmsSender>().AsSingleton().AlsoAs<IMessageSender>().WithKey("sms");

        using (var container = builder.Build())
        {
            Console.WriteLine(container.Resolve<IMessageSender>().Send("hello"));
            Console.WriteLine(container.Resolve<string>());
            Console.WriteLine($"factory ran at {container.Resolve<DateTime>():HH:mm:ss}");
            Console.WriteLine(container.Resolve<SmsSender>("sms").Send("same class, own registration"));
        }

        // Replace swaps the winning registration, which is what tests and profiles do.
        var replaced = new ContainerBuilder();
        replaced.Register<IMessageSender, EmailSender>().AsSingleton();
        replaced.Replace<IMessageSender, SmsSender>().AsSingleton();
        using (var container = replaced.Build())
            Console.WriteLine("after Replace: " + container.Resolve<IMessageSender>().Send("hello"));

        // ExternallyOwned: the container hands the object out but will not dispose it.
        var owned = new ContainerBuilder();
        owned.RegisterInstance(new EmailSender()).ExternallyOwned();
        using (var container = owned.Build())
            container.Resolve<EmailSender>();
        Console.WriteLine("externally owned instance survived container disposal");

        return Task.CompletedTask;
    }
}
