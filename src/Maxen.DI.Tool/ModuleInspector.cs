namespace Maxen.DI.Tool;

/// <summary>
/// Loads an assembly in its own context, builds a container from its modules and hands it to a callback.
/// Every command shares this, so the "file missing", "no modules" and "cannot load" messages are the same everywhere.
/// </summary>
internal static class ModuleInspector
{
    /// <summary>Runs <paramref name="inspect"/> against the container; returns its exit code, or 2 if the assembly could not be inspected.</summary>
    public static int Inspect(string assemblyPath, IReadOnlyList<string> profiles, TextWriter error, Func<Container, int> inspect)
    {
        var path = Path.GetFullPath(assemblyPath);
        if (!File.Exists(path))
        {
            error.WriteLine($"Assembly not found: {path}");
            return 2;
        }

        // Its own load context, so the assembly's dependencies don't clash with ours and everything can be unloaded after.
        var context = new ModuleLoadContext(path);
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);

            var builder = new ContainerBuilder();
            builder.RegisterModules(assembly);
            foreach (var profile in profiles) builder.UseProfile(profile);

            using var container = builder.Build();

            if (container.BuildGraph().Nodes.Count <= 3)
            {
                // The three built-ins (scope, provider, container) are always there; anything more came from modules.
                error.WriteLine($"No registrations found. '{assembly.GetName().Name}' needs at least one public IContainerModule with a parameterless constructor.");
                return 2;
            }

            return inspect(container);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or RegistrationException)
        {
            error.WriteLine($"Could not inspect '{path}': {ex.Message}");
            return 2;
        }
        finally
        {
            context.Unload();
        }
    }
}
