using Maxen.DI.Pipeline;
using System.Linq.Expressions;
using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// Everything needed to call one constructor, worked out once: the parameter list, the service key
/// each parameter asks for, and compiled delegates that call the constructor. Looking these up per
/// resolve (GetParameters copies an array, attribute reads allocate) was a noticeable cost.
/// </summary>
internal sealed class ConstructorPlan
{
    private Func<object?[], object>? _fromArray;
    private Func<ResolveContext, object>? _direct;
    private Delegate? _fixed;
    private bool _fixedTried;

    public ConstructorPlan(ConstructorInfo constructor)
    {
        Constructor = constructor;
        Parameters = constructor.GetParameters();

        Keys = new ServiceKey[Parameters.Length];
        for (var i = 0; i < Parameters.Length; i++)
            Keys[i] = DefaultParameterResolver.KeyOf(Parameters[i]);
    }

    public ConstructorInfo Constructor { get; }
    public ParameterInfo[] Parameters { get; }

    /// <summary>The service key each parameter asks for, in parameter order.</summary>
    public ServiceKey[] Keys { get; }

    /// <summary>
    /// <c>args =&gt; new T((A)args[0], ...)</c>, for callers that gather the arguments themselves
    /// (custom parameter resolvers, per-call arguments, decorator overrides). Compiled on first use.
    /// </summary>
    public Func<object?[], object> FromArray => _fromArray ??= CompileFromArray();

    /// <summary>
    /// <c>ctx =&gt; new T((A)Resolve(ctx, ...), ...)</c>: resolves each dependency itself, so there is no
    /// argument array and no resolver chain. Only valid when the default resolver is the only one.
    /// </summary>
    public Func<ResolveContext, object> Direct => _direct ??= CompileDirect();

    /// <summary>
    /// A delegate taking the arguments directly (<c>Func&lt;object, object, object&gt;</c> for two parameters, and so on up
    /// to four), so building an object needs no argument array. Null where that isn't possible: no parameters, more than
    /// four, or native AOT, where compiled expressions are interpreted and the generated array-based call is better.
    /// </summary>
    public Delegate? Fixed
    {
        get
        {
            if (_fixedTried) return _fixed;

            if (Parameters.Length is >= 1 and <= 4
                && System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
                && !GeneratedActivators.PreferForAllConstruction)
                _fixed = CompileFixed();

            _fixedTried = true;
            return _fixed;
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only called when RuntimeFeature.IsDynamicCodeCompiled is true, i.e. never under native AOT.")]
    private Delegate CompileFixed()
    {
        var inputs = Parameters.Select((p, i) => Expression.Parameter(typeof(object), "a" + i)).ToArray();
        var arguments = inputs.Select((input, i) => (Expression)Expression.Convert(input, Parameters[i].ParameterType));
        var body = Expression.Convert(Expression.New(Constructor, arguments), typeof(object));

        var delegateType = Expression.GetFuncType(inputs.Select(_ => typeof(object)).Append(typeof(object)).ToArray());
        return Expression.Lambda(delegateType, body, inputs).Compile();
    }

    private Func<object?[], object> CompileFromArray()
    {
        // The generator may already have written this exact constructor call; no expression tree needed then.
        if (GeneratedActivators.TryGet(Constructor, Parameters, out var generated))
            return generated;

        var array = Expression.Parameter(typeof(object[]), "args");

        var arguments = Parameters.Select((p, i) =>
            (Expression)Expression.Convert(Expression.ArrayIndex(array, Expression.Constant(i)), p.ParameterType));

        var body = Expression.Convert(Expression.New(Constructor, arguments), typeof(object));
        return Expression.Lambda<Func<object?[], object>>(body, array).Compile();
    }

    private Func<ResolveContext, object> CompileDirect()
    {
        // Where compiled expressions are slow (native AOT), resolve the arguments ourselves and hand them to
        // the generated constructor call. It costs one array per object, but beats an interpreted tree.
        if (GeneratedActivators.PreferForAllConstruction && GeneratedActivators.TryGet(Constructor, Parameters, out var generated))
            return ctx =>
            {
                var arguments = new object?[Parameters.Length];
                for (var i = 0; i < arguments.Length; i++)
                    arguments[i] = DefaultParameterResolver.ResolveDirect(Parameters[i], Keys[i], ctx);
                return generated(arguments);
            };

        var context = Expression.Parameter(typeof(ResolveContext), "ctx");
        var resolve = typeof(DefaultParameterResolver).GetMethod(
            nameof(DefaultParameterResolver.ResolveDirect), BindingFlags.Static | BindingFlags.NonPublic)!;

        // The parameter info and key are baked in as constants, so nothing is looked up per call.
        var arguments = Parameters.Select((p, i) => (Expression)Expression.Convert(
            Expression.Call(resolve, Expression.Constant(p), Expression.Constant(Keys[i]), context),
            p.ParameterType));

        var body = Expression.Convert(Expression.New(Constructor, arguments), typeof(object));
        return Expression.Lambda<Func<ResolveContext, object>>(body, context).Compile();
    }
}
