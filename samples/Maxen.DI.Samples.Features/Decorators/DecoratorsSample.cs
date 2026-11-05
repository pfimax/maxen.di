namespace Maxen.DI.Samples.Features.Decorators;

internal sealed class DecoratorsSample : ISample
{
    public string Name => "decorators";
    public string Summary => "wrap a service without changing it, stacked by priority";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<IGreeter, Greeter>();
        builder.Decorate<IGreeter, ExcitedGreeter>();
        builder.Decorate<IGreeter, PoliteGreeter>();

        using var container = builder.Build();
        Console.WriteLine(container.Resolve<IGreeter>().Greet("Ada"));
        return Task.CompletedTask;
    }
}
