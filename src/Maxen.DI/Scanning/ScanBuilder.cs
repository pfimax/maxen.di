using System.Runtime.CompilerServices;

namespace Maxen.DI.Scanning;

/// <summary>
/// Describes one scanning rule: which classes to pick, which services to register them as,
/// and with which lifetime. Chain the calls, for example:
/// <c>s.InNamespace("MyApp.Services").AsImplementedInterfaces().AsSingleton()</c>.
/// </summary>
public sealed class ScanBuilder
{
    private readonly List<Func<Type, bool>> _filters = new();
    private readonly List<Func<Type, IEnumerable<Type>>> _serviceSelectors = new();
    private Lifetime _lifetime = Lifetime.Transient;
    private bool _includeNonPublic;

    /// <summary>Only classes for which <paramref name="predicate"/> is true. Calling it again narrows the selection further.</summary>
    public ScanBuilder Where(Func<Type, bool> predicate)
    {
        _filters.Add(predicate ?? throw new ArgumentNullException(nameof(predicate)));
        return this;
    }

    /// <summary>Classes in this namespace or any namespace below it.</summary>
    public ScanBuilder InNamespace(string @namespace)
        => Where(t => t.Namespace is not null
            && (t.Namespace == @namespace || t.Namespace.StartsWith(@namespace + ".", StringComparison.Ordinal)));

    /// <summary>Keeps only types that can be assigned to <typeparamref name="T"/>.</summary>
    public ScanBuilder AssignableTo<T>() => AssignableTo(typeof(T));

    /// <summary>
    /// Classes that implement or derive from <paramref name="type"/>. Open generics such as
    /// <c>typeof(IHandler&lt;&gt;)</c> match every closed form.
    /// </summary>
    public ScanBuilder AssignableTo(Type type)
        => Where(t => type.IsGenericTypeDefinition ? ImplementsOpenGeneric(t, type) : type.IsAssignableFrom(t));

    /// <summary>Keeps only types whose name ends with the given text.</summary>
    public ScanBuilder NameEndsWith(string suffix)
        => Where(t => t.Name.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>By default internal and nested private classes are skipped. This includes them.</summary>
    public ScanBuilder IncludeNonPublicTypes()
    {
        _includeNonPublic = true;
        return this;
    }

    /// <summary>Register each class as itself.</summary>
    public ScanBuilder AsSelf() => As(t => new[] { t });

    /// <summary>
    /// Register each class as the public interfaces it implements (System.* interfaces such as
    /// <c>IDisposable</c> are ignored).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Scanning is already marked as unsafe for trimming, and only interfaces of scanned types are read.")]
    public ScanBuilder AsImplementedInterfaces()
        => As(t => t.GetInterfaces().Where(i => i.IsPublic && i.Namespace?.StartsWith("System", StringComparison.Ordinal) != true));

    /// <summary>Register each class as whatever types you choose. Can be combined with the other As methods.</summary>
    public ScanBuilder As(Func<Type, IEnumerable<Type>> serviceTypes)
    {
        _serviceSelectors.Add(serviceTypes ?? throw new ArgumentNullException(nameof(serviceTypes)));
        return this;
    }

    /// <summary>Registers what was found as transient.</summary>
    public ScanBuilder AsTransient() => WithLifetime(Lifetime.Transient);
    /// <summary>Registers what was found as scoped.</summary>
    public ScanBuilder AsScoped() => WithLifetime(Lifetime.Scoped);
    /// <summary>Registers what was found as singletons.</summary>
    public ScanBuilder AsSingleton() => WithLifetime(Lifetime.Singleton);

    /// <summary>Registers what was found with a custom lifetime.</summary>
    public ScanBuilder WithLifetime(Lifetime lifetime)
    {
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        return this;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Scanning is already marked RequiresUnreferencedCode, so callers accepted this.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Scanning is already marked RequiresUnreferencedCode, so callers accepted this.")]
    internal void Apply(ContainerBuilder builder, IEnumerable<Type> candidates)
    {
        // With no As* call, the sensible default is "register the class as itself".
        var selectors = _serviceSelectors.Count > 0
            ? _serviceSelectors
            : new List<Func<Type, IEnumerable<Type>>> { t => new[] { t } };

        foreach (var type in candidates.Where(IsCandidate))
        {
            var services = selectors.SelectMany(select => select(type)).Distinct().ToList();

            // Open generic classes can't be built without knowing their type arguments.
            if (type.IsGenericTypeDefinition)
            {
                RegisterOpenGeneric(builder, type, services);
                continue;
            }

            if (services.Count == 0) continue;

            // One registration plus aliases: a singleton stays a single object across all its interfaces.
            var registration = builder.Register(services[0], type).WithLifetime(_lifetime);
            foreach (var extra in services.Skip(1))
                registration.AlsoAs(extra);
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Scanning is already marked RequiresUnreferencedCode, so callers accepted this.")]
    private void RegisterOpenGeneric(ContainerBuilder builder, Type type, List<Type> services)
    {
        // Each open interface it implements becomes its own open registration (IRepo<> -> Repo<>).
        // Reflection reports them as IRepo<T> (bound to the class's own T), so reduce to the open definition.
        var openServices = services
            .Where(s => s.IsGenericType)
            .Select(s => s.IsGenericTypeDefinition ? s : s.GetGenericTypeDefinition())
            .Distinct();

        foreach (var service in openServices)
            builder.Register(service, type).WithLifetime(_lifetime);
    }

    private bool IsCandidate(Type type)
    {
        if (!type.IsClass || type.IsAbstract) return false;
        if (type.IsDefined(typeof(CompilerGeneratedAttribute), false)) return false;
        if (!_includeNonPublic && !(type.IsPublic || type.IsNestedPublic)) return false;
        return _filters.All(f => f(type));
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Scanning is already marked as unsafe for trimming, and only interfaces of scanned types are read.")]
    private static bool ImplementsOpenGeneric(Type type, Type openGeneric)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == openGeneric) return true;

        return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
    }
}
