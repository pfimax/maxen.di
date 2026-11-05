using System.Reflection;

namespace Maxen.DI.Scanning;

/// <summary>Fluent assembly scanning for <see cref="ContainerBuilder"/>.</summary>
public static class ScanExtensions
{
    /// <summary>Scans one assembly. Call it several times to apply different rules to different groups of classes.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection, so types nothing else references may be trimmed away. Register services explicitly or use the source generator when trimming.")]
    public static ContainerBuilder Scan(this ContainerBuilder builder, Assembly assembly, Action<ScanBuilder> configure)
        => builder.Scan(new[] { assembly }, configure);

    /// <summary>Scans several assemblies with one set of rules.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection, so types nothing else references may be trimmed away. Register services explicitly or use the source generator when trimming.")]
    public static ContainerBuilder Scan(this ContainerBuilder builder, IEnumerable<Assembly> assemblies, Action<ScanBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(configure);

        var rule = new ScanBuilder();
        configure(rule);

        // Some assemblies can only be partially loaded; scan what we can instead of failing outright.
        var types = assemblies.SelectMany(SafeGetTypes).ToList();
        rule.Apply(builder, types);
        return builder;
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection.")]
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
