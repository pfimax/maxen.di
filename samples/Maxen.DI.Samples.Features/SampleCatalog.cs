namespace Maxen.DI.Samples.Features;

/// <summary>Finds every <see cref="ISample"/> in this assembly, so adding a sample is just adding a class.</summary>
internal static class SampleCatalog
{
    public static IReadOnlyList<ISample> All()
        => typeof(SampleCatalog).Assembly.GetTypes()
            .Where(t => typeof(ISample).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .Select(t => (ISample)Activator.CreateInstance(t)!)
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToArray();
}
