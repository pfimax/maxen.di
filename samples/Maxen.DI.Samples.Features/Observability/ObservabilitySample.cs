using System.Diagnostics;
using Maxen.DI.Diagnostics;
using Maxen.DI.Samples.Features.Validation;

namespace Maxen.DI.Samples.Features.Observability;

internal sealed class ObservabilitySample : ISample
{
    public string Name => "observability";
    public string Summary => "resolve traces, and OpenTelemetry-ready activities and metrics under the name Maxen.DI";

    public Task RunAsync()
    {
        // 1) A trace of what was built, in order, with timings.
        var trace = new ResolutionTrace();
        var builder = new ContainerBuilder();
        builder.Register<Database>().AsSingleton();
        builder.Register<UserRepository>();
        builder.Observe(trace);

        using (var container = builder.Build())
            container.Resolve<UserRepository>();

        Console.Write(trace);

        // 2) Telemetry: any ActivityListener (OpenTelemetry uses one) can subscribe to the "Maxen.DI" source.
        var spans = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MaxenTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => spans.Add(activity.DisplayName),
        };
        ActivitySource.AddActivityListener(listener);

        var traced = new ContainerBuilder();
        traced.UseTelemetry();
        traced.Register<Database>();
        using (var container = traced.Build())
            container.Resolve<Database>();

        Console.WriteLine($"telemetry recorded {spans.Count} span(s): {string.Join(", ", spans)}");
        return Task.CompletedTask;
    }
}
