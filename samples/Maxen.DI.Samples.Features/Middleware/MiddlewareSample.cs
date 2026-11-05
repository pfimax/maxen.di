using Maxen.DI.Pipeline;

namespace Maxen.DI.Samples.Features.Middleware;

internal sealed class MiddlewareSample : ISample
{
    public string Name => "middleware";
    public string Summary => "hook into every resolve, or into one registration (Use, OnActivated)";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();

        // Runs around every resolve of every service; call next() to continue down the pipeline.
        builder.Use(MiddlewarePhase.PreLifetime, (context, next) =>
        {
            Console.WriteLine("  resolving " + context.Key);
            return next(context);
        });

        // Runs only when this one registration builds a new instance.
        builder.Register<Stamp>().OnActivated((instance, _) =>
        {
            ((Stamp)instance).CreatedAt = new DateTime(2030, 1, 1);
            Console.WriteLine("  Stamp activated");
        });

        using var container = builder.Build();
        Console.WriteLine("created at " + container.Resolve<Stamp>().CreatedAt.Year);
        return Task.CompletedTask;
    }
}
