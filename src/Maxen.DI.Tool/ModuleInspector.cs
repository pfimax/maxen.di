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
            Container container;
            string assemblyName;
            try
            {
                var assembly = context.LoadFromAssemblyPath(path);
                assemblyName = assembly.GetName().Name ?? path;

                var builder = new ContainerBuilder();
                builder.RegisterModules(assembly);
                foreach (var profile in profiles) builder.UseProfile(profile);
                container = builder.Build();
            }
            catch (Exception ex)
            {
                // Everything in here runs the inspected app's own code: its module constructors and Configure methods,
                // and types that may need an assembly that isn't next to it. Whatever goes wrong is that app's problem,
                // so it gets a plain message and exit code 2 rather than a stack trace from inside the tool.
                error.WriteLine($"Could not inspect '{path}': {Describe(ex)}");
                return 2;
            }

            using (container)
            {
                try
                {
                    if (container.BuildGraph().Nodes.Count <= 3)
                    {
                        // The three built-ins (scope, provider, container) are always there; anything more came from modules.
                        error.WriteLine($"No registrations found. '{assemblyName}' needs at least one public IContainerModule with a parameterless constructor.");
                        return 2;
                    }

                    return inspect(container);
                }
                catch (Exception ex) when (IsLoadFailure(ex))
                {
                    // Reading constructors can still reach a type whose assembly is missing. Anything else thrown here
                    // would be a bug in the tool itself, which should stay loud.
                    error.WriteLine($"Could not inspect '{path}': {Describe(ex)}");
                    return 2;
                }
            }
        }
        finally
        {
            context.Unload();
        }
    }

    private static bool IsLoadFailure(Exception ex)
        => ex is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException
            or System.Reflection.ReflectionTypeLoadException;

    // The useful part of the exception, without the wrappers reflection puts around it.
    private static string Describe(Exception ex)
    {
        if (ex is System.Reflection.TargetInvocationException { InnerException: { } inner })
            return Describe(inner);

        // "Unable to load one or more of the requested types" says nothing; the first loader error names the assembly.
        if (ex is System.Reflection.ReflectionTypeLoadException load
            && load.LoaderExceptions.FirstOrDefault(e => e is not null) is { } first)
            return first.Message;

        // The container's own errors (an undefined profile, a bad registration) and load failures read fine as they are.
        if (ex is DIException || IsLoadFailure(ex))
            return ex.Message;

        return $"{ex.GetType().Name}: {ex.Message}";
    }
}
