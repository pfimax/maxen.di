using Maxen.DI.Samples.Features.Registration;

namespace Maxen.DI.Samples.Features.Profiles;

internal sealed class ProfilesSample : ISample
{
    public string Name => "profiles";
    public string Summary => "named sets of registrations that override the defaults when switched on";

    public Task RunAsync()
    {
        ContainerBuilder Configure(bool development)
        {
            var builder = new ContainerBuilder();
            builder.Register<IMessageSender, EmailSender>();
            builder.DefineProfile("Development", p => p.Replace<IMessageSender, SmsSender>());
            // In real apps this is UseProfileFromEnvironment(), reading MAXEN_PROFILE.
            if (development) builder.UseProfile("Development");
            return builder;
        }

        using var production = Configure(false).Build();
        using var development = Configure(true).Build();
        Console.WriteLine("without the profile: " + production.Resolve<IMessageSender>().Send("hi"));
        Console.WriteLine("with Development:    " + development.Resolve<IMessageSender>().Send("hi"));
        return Task.CompletedTask;
    }
}
