using Maxen.DI.Diagnostics;
using Maxen.DI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Maxen.DI reports through System.Diagnostics, so OpenTelemetry only needs to be told the name to listen to.
// MaxenTelemetry.Name is that name; it is used for both the traces and the metrics.
using var tracing = Sdk.CreateTracerProviderBuilder()
    .AddSource(MaxenTelemetry.Name)
    .AddConsoleExporter()
    .Build();

using var metrics = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MaxenTelemetry.Name)
    .AddConsoleExporter()
    .Build();

var builder = new ContainerBuilder();
builder.Register<Repository>().AsSingleton();
builder.Register<Service>().AsScoped();
builder.UseTelemetry();

using var container = builder.Build();

// Each resolve becomes a span, nested like the dependency chain: Service contains Repository.
using (var scope = container.CreateScope())
{
    scope.Resolve<Service>().Run();
}

// Metrics are exported in batches; flush so the run does not end before they appear.
metrics.ForceFlush();

internal sealed class Repository
{
    public string Load() => "hello";
}

internal sealed class Service
{
    private readonly Repository _repository;

    public Service(Repository repository) => _repository = repository;

    public void Run() => Console.WriteLine(_repository.Load());
}
