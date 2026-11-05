using Maxen.DI.Options;

namespace Maxen.DI.Samples.Features.Options;

internal sealed class OptionsSample : ISample
{
    public string Name => "options";
    public string Summary => "typed settings from code or from key/value pairs, as IOptions<T>";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Configure<MailOptions>(o => o.Port = 587);

        using (var container = builder.Build())
        {
            var options = container.Resolve<IOptions<MailOptions>>().Value;
            Console.WriteLine($"from code: {options.Host}:{options.Port}");
        }

        // Flat key/value pairs, the shape configuration files and environment variables are read into.
        var bound = new ContainerBuilder();
        bound.BindOptions<MailOptions>(new Dictionary<string, string?>
        {
            ["Mail:Host"] = "smtp.example.com",
            ["Mail:Port"] = "2525",
        }, section: "Mail");

        using var second = bound.Build();
        var value = second.Resolve<IOptions<MailOptions>>().Value;
        Console.WriteLine($"from pairs: {value.Host}:{value.Port}");
        return Task.CompletedTask;
    }
}
