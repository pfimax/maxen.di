using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// A <c>Lazy&lt;T&gt;</c> or <c>Func&lt;..., T&gt;</c> parameter. Nothing is resolved while the plan runs: the wrapper
/// just remembers the scope it was created in and resolves T later, exactly as the framework's own sources do.
/// </summary>
internal sealed class DeferredPlanNode : PlanNode
{
    private readonly Type _wrapperType;
    private readonly ServiceKey _inner;
    private readonly bool _isLazy;

    public DeferredPlanNode(Type wrapperType, ServiceKey inner, bool isLazy)
    {
        _wrapperType = wrapperType;
        _inner = inner;
        _isLazy = isLazy;
    }

    /// <summary>The kind of wrapper a type is and what it produces, or false for any other type.</summary>
    public static bool TryDescribe(ServiceKey key, out ServiceKey inner, out bool isLazy)
    {
        inner = default;
        isLazy = false;

        var type = key.Type;
        if (!type.IsGenericType) return false;

        var arguments = type.GetGenericArguments();
        isLazy = type.GetGenericTypeDefinition() == typeof(Lazy<>);

        // Func<A, B, T> produces T, the last argument; four arguments is the most the framework supports.
        var isFunc = arguments.Length <= 5 && type.GetGenericTypeDefinition().FullName == "System.Func`" + arguments.Length;
        if (!isLazy && !isFunc) return false;

        inner = new ServiceKey(arguments[^1], key.Key);
        return true;
    }

    /// <inheritdoc/>
    public override object Get(IScope scope)
        => _isLazy ? LazySource.Create(_wrapperType, scope, _inner) : FuncSource.Create(_wrapperType, scope, _inner);
}
