using Maxen.DI.Samples.Features.Scanning.Plugins;
using Maxen.DI.Scanning;

namespace Maxen.DI.Samples.Features.Scanning;

internal sealed class ScanningSample : ISample
{
    public string Name => "scanning";
    public string Summary => "register many types at once by namespace, name or attribute";

    public Task RunAsync()
    {
        var assembly = typeof(ScanningSample).Assembly;

        var builder = new ContainerBuilder();
        builder.Scan(assembly, scan => scan
            .InNamespace("Maxen.DI.Samples.Features.Scanning.Plugins")
            .AssignableTo<IPlugin>()
            .AsImplementedInterfaces()
            .AsSingleton());
        builder.RegisterAttributed(assembly);

        using var container = builder.Build();
        Console.WriteLine("plugins: " + string.Join(", ", container.ResolveAll<IPlugin>().Select(p => p.Name)));
        Console.WriteLine("attributed: " + container.Resolve<AttributedAudit>().Describe());
        return Task.CompletedTask;
    }
}
