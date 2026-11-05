using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Features.Interception;

internal sealed class InterceptionSample : ISample
{
    public string Name => "interception";
    public string Summary => "run code around every method call (logging, timing, caching)";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        // The interceptor is an ordinary service, so it is registered like any other.
        builder.Register<CallLogger>();
        builder.Register<ICalculator, Calculator>().InterceptWith<CallLogger>();

        using var container = builder.Build();
        Console.WriteLine("result: " + container.Resolve<ICalculator>().Add(2, 3));

        // The same thing for a service registered elsewhere, from the builder.
        var other = new ContainerBuilder();
        other.Register<CallLogger>();
        other.Register<ICalculator, Calculator>();
        other.Intercept<ICalculator, CallLogger>();
        using var second = other.Build();
        Console.WriteLine("result: " + second.Resolve<ICalculator>().Add(10, 20));
        return Task.CompletedTask;
    }
}
