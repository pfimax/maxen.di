using System.Reflection;
using System.Runtime.Loader;

namespace Maxen.DI.Tool;

/// <summary>
/// Loads the assembly being inspected together with its own dependencies, while leaving Maxen.DI itself to the tool.
/// Sharing one copy matters: if the target brought its own, its modules would implement a different IContainerModule
/// than the one this tool looks for.
/// </summary>
internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public ModuleLoadContext(string assemblyPath) : base("maxen-di-target", isCollectible: true)
        => _resolver = new AssemblyDependencyResolver(assemblyPath);

    protected override Assembly? Load(AssemblyName name)
    {
        // Returning null hands the request to the default context, which already has our Maxen.DI.
        if (name.Name == "Maxen.DI") return null;

        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
