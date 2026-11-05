namespace Maxen.DI.Samples.Features.Injection;

internal sealed class InjectionSample : ISample
{
    public string Name => "injection";
    public string Summary => "property and method injection (off by default, constructors stay the main way)";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<Settings>();
        builder.Register<Component>();
        builder.UsePropertyInjection();
        builder.UseMethodInjection();

        using var container = builder.Build();
        var component = container.Resolve<Component>();
        Console.WriteLine("property: " + component.Settings?.Environment);
        Console.WriteLine("method:   " + component.Source);
        return Task.CompletedTask;
    }
}
