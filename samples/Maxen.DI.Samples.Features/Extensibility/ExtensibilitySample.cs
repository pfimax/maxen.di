namespace Maxen.DI.Samples.Features.Extensibility;

internal sealed class ExtensibilitySample : ISample
{
    public string Name => "extensibility";
    public string Summary => "custom lifetime, registration source, parameter resolver and initializer";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<Token>().WithLifetime(new WindowedLifetime(TimeSpan.FromMilliseconds(100)));
        builder.AddSource(new BannerSource());
        builder.AddParameterResolver(new ConnectionStringResolver());
        builder.AddInitializer(new StampInitializer());
        builder.Register<DataClient>();

        using var container = builder.Build();

        var first = container.Resolve<Token>().Number;
        var second = container.Resolve<Token>().Number;
        Thread.Sleep(150);
        var third = container.Resolve<Token>().Number;
        Console.WriteLine($"custom lifetime: {first}, {second} (same window), {third} (window expired)");

        Console.WriteLine("source:      " + container.Resolve<IBanner>().Text);

        var client = container.Resolve<DataClient>();
        Console.WriteLine($"resolver:    {client.ConnectionString}");
        Console.WriteLine($"initializer: stamped {client.Stamped:yyyy}");
        return Task.CompletedTask;
    }
}
