namespace Maxen.DI.Internal;

/// <summary>Works out concrete generic types, e.g. which <c>Repo&lt;T&gt;</c> to build for <c>IRepo&lt;Customer&gt;</c>.</summary>
internal static class OpenGenericCloser
{
    /// <summary>True if the open type implements or derives from the generic definition (e.g. <c>Repo&lt;&gt;</c> and <c>IRepo&lt;&gt;</c>).</summary>
    public static bool Implements(Type openImplementation, Type serviceDefinition)
        => FindTemplate(openImplementation, serviceDefinition) is not null;

    /// <summary>
    /// Closes <paramref name="openImplementation"/> so it fits <paramref name="closedService"/>:
    /// <c>Repo&lt;&gt;</c> + <c>IRepo&lt;Foo&gt;</c> gives <c>Repo&lt;Foo&gt;</c>. Returns null if that isn't
    /// possible, for example when a generic constraint doesn't hold.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Closed generics are only built for types the application registers, so their members are already preserved.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Same as above: the closed type is one the application already uses, so its code was generated ahead of time.")]
    public static Type? Close(Type openImplementation, Type closedService)
    {
        if (!openImplementation.IsGenericTypeDefinition) return openImplementation;
        if (!closedService.IsConstructedGenericType) return null;

        var template = FindTemplate(openImplementation, closedService.GetGenericTypeDefinition());
        if (template is null) return null;

        // Match the template (IRepo<T>) against the real service (IRepo<Foo>) to learn that T = Foo.
        var bindings = new Dictionary<Type, Type>();
        if (!Unify(template, closedService, bindings)) return null;

        var parameters = openImplementation.GetGenericArguments();
        var arguments = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!bindings.TryGetValue(parameters[i], out var argument)) return null;
            arguments[i] = argument;
        }

        try
        {
            return openImplementation.MakeGenericType(arguments);
        }
        catch (ArgumentException)
        {
            // A generic constraint (where T : class, ...) rejected these arguments.
            return null;
        }
    }

    /// <summary>Finds the base type or interface of the implementation that matches the service definition.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Only interface lists of registered types are read.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Only interface lists of registered types are read.")]
    private static Type? FindTemplate(Type openImplementation, Type definition)
    {
        for (var type = openImplementation; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
                return type;

            foreach (var contract in type.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == definition)
                    return contract;
        }
        return null;
    }

    /// <summary>Walks both types side by side and records what each generic parameter stands for.</summary>
    private static bool Unify(Type template, Type actual, Dictionary<Type, Type> bindings)
    {
        if (template.IsGenericParameter)
        {
            // Seeing the same parameter twice (like IMap<T, T>) means both sides must agree.
            if (bindings.TryGetValue(template, out var existing)) return existing == actual;
            bindings[template] = actual;
            return true;
        }

        if (!template.ContainsGenericParameters) return template == actual;

        if (template.IsArray)
            return actual.IsArray
                && template.GetArrayRank() == actual.GetArrayRank()
                && Unify(template.GetElementType()!, actual.GetElementType()!, bindings);

        if (template.IsGenericType)
        {
            if (!actual.IsGenericType || template.GetGenericTypeDefinition() != actual.GetGenericTypeDefinition())
                return false;

            var templateArgs = template.GetGenericArguments();
            var actualArgs = actual.GetGenericArguments();
            for (var i = 0; i < templateArgs.Length; i++)
                if (!Unify(templateArgs[i], actualArgs[i], bindings)) return false;
            return true;
        }

        return false;
    }
}
