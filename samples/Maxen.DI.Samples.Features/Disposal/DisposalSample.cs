namespace Maxen.DI.Samples.Features.Disposal;

internal sealed class DisposalSample : ISample
{
    public string Name => "disposal";
    public string Summary => "who disposes what: scopes dispose what they built, newest first";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<Resource>().AsTransient();

        using var container = builder.Build();

        Console.WriteLine("ending a scope that built three resources:");
        using (var scope = container.CreateScope())
        {
            scope.Resolve<Resource>();
            scope.Resolve<Resource>();
            scope.Resolve<Resource>();
        }

        return Task.CompletedTask;
    }
}
