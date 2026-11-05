namespace Maxen.DI.Samples.Features.Deferred;

internal sealed class DeferredSample : ISample
{
    public string Name => "deferred";
    public string Summary => "Lazy<T>, Func<T>, Func<arg, T> and ResolveWith for runtime values";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<ExpensiveReport>();
        builder.Register<Order>().WithRuntimeArguments();
        builder.Register<Dashboard>();

        using var container = builder.Build();
        using var scope = container.CreateScope();

        Console.WriteLine("resolving the dashboard...");
        var dashboard = scope.Resolve<Dashboard>();
        Console.WriteLine("dashboard ready, nothing expensive built yet");

        Console.WriteLine("Lazy value: " + dashboard.Report.Value.Title);
        Console.WriteLine("Func call:  " + dashboard.NewReport().Title);

        // The int comes from the caller, the report from the container.
        Console.WriteLine("Func<int, Order>: order #" + dashboard.OrderFactory(42).Id);
        Console.WriteLine("ResolveWith:      order #" + scope.ResolveWith<Order>(new { id = 7 }).Id);
        return Task.CompletedTask;
    }
}
