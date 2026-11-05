using Maxen.DI.Diagnostics;
using Maxen.DI.Samples.Features.Validation;

namespace Maxen.DI.Samples.Features.Graph;

internal sealed class GraphSample : ISample
{
    public string Name => "graph";
    public string Summary => "draw who depends on whom (Mermaid, DOT or text); the maxen-di tool does the same for a DLL";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<Database>().AsSingleton();
        builder.Register<UserRepository>().AsScoped();

        using var container = builder.Build();
        var graph = container.BuildGraph();

        Console.WriteLine(graph.ToMermaid());
        Console.WriteLine(graph.ToText());
        return Task.CompletedTask;
    }
}
