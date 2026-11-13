using Maxen.DI.Pipeline;
using System.Linq.Expressions;
using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// Everything needed to call one constructor, worked out once: the parameter list, the service key
/// each parameter asks for, and delegates that call the constructor. Looking these up per
/// resolve (GetParameters copies an array, attribute reads allocate) was a noticeable cost.
/// </summary>
/// <remarks>
/// A compiled call is the fastest way to run a constructor, but compiling one takes tens of microseconds, far longer
/// than the constructor itself. Most services are singletons, or are built a handful of times, and would never earn that
/// back; paying it for every service is what made the first resolves of an application slow. So a constructor starts
/// out being called through reflection, and gets a compiled call, made on a background thread, once it has been used
/// <see cref="CompileAfter"/> times. Both ways build the same object and fail the same way.
/// </remarks>
internal sealed class ConstructorPlan
{
    /// <summary>The use of a constructor on which a compiled call is ordered for it.</summary>
    internal const int CompileAfter = 2;

    // One way of calling the constructor: the compiled delegate once there is one, the stand-in until then.
    private sealed class Tier<T> where T : class
    {
        public T? Fast;
        public T? Slow;
        public int Uses;
    }

    private readonly Tier<Func<object?[], object>> _fromArray = new();
    private readonly Tier<Func<ResolveContext, object>> _direct = new();
    private readonly Tier<Delegate> _fixed = new();

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
    /// (custom parameter resolvers, per-call arguments, decorator overrides).
    /// </summary>
    public Func<object?[], object> FromArray => _fromArray.Fast ?? WarmFromArray();

    /// <summary>
    /// <c>ctx =&gt; new T((A)Resolve(ctx, ...), ...)</c>: resolves each dependency itself, so there is no
    /// argument array and no resolver chain. Only valid when the default resolver is the only one.
    /// </summary>
    public Func<ResolveContext, object> Direct => _direct.Fast ?? WarmDirect();

    /// <summary>
    /// A delegate taking the arguments directly (<c>Func&lt;object, object, object&gt;</c> for two parameters, and so on up
    /// to four), so building an object needs no argument array. Null where that isn't possible: no parameters, more than
    /// four, or native AOT, where compiled expressions are interpreted and the generated array-based call is better.
    /// Also null until the constructor has been used often enough to be compiled; <see cref="FromArray"/> covers that time.
    /// </summary>
    public Delegate? Fixed
    {
        get
        {
            var fast = _fixed.Fast;
            if (fast is not null) return fast;

            if (Parameters.Length is >= 1 and <= 4
                && System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled
                && !GeneratedActivators.PreferForAllConstruction
                && Interlocked.Increment(ref _fixed.Uses) == CompileAfter)
                CompileInBackground(_fixed, CompileFixed);

            return null;
        }
    }

    private Func<object?[], object> WarmFromArray()
    {
        // The generator may already have written this exact constructor call: as good as compiled, and free.
        if (GeneratedActivators.TryGet(Constructor, Parameters, out var generated))
        {
            Volatile.Write(ref _fromArray.Fast, generated);
            return generated;
        }

        if (Interlocked.Increment(ref _fromArray.Uses) == CompileAfter)
            CompileInBackground(_fromArray, CompileFromArray);

        return _fromArray.Slow ??= Invoke;
    }

    private Func<ResolveContext, object> WarmDirect()
    {
        // Where compiled expressions are slow (native AOT), resolve the arguments ourselves and hand them to
        // the generated constructor call. It costs one array per object, but beats an interpreted tree.
        if (GeneratedActivators.PreferForAllConstruction && GeneratedActivators.TryGet(Constructor, Parameters, out var generated))
        {
            Func<ResolveContext, object> viaGenerated = ctx => generated(ResolveArguments(ctx));
            Volatile.Write(ref _direct.Fast, viaGenerated);
            return viaGenerated;
        }

        if (Interlocked.Increment(ref _direct.Uses) == CompileAfter)
            CompileInBackground(_direct, CompileDirect);

        return _direct.Slow ??= ctx => Invoke(ResolveArguments(ctx));
    }

    private object?[] ResolveArguments(ResolveContext context)
    {
        if (Parameters.Length == 0) return Array.Empty<object?>();

        var arguments = new object?[Parameters.Length];
        for (var i = 0; i < arguments.Length; i++)
            arguments[i] = DefaultParameterResolver.ResolveDirect(Parameters[i], Keys[i], context);
        return arguments;
    }

    /// <summary>Calls the constructor through reflection: slower per call than a compiled delegate, but ready at once.</summary>
    private object Invoke(object?[] arguments)
    {
        // A compiled call casts each argument, so a value of the wrong type is an InvalidCastException and a null for
        // a value type a NullReferenceException. Reflection would report the first differently and quietly turn the
        // second into a zero. Which of the two ways is in use is our business, so both must fail alike.
        for (var i = 0; i < arguments.Length; i++)
        {
            var type = Parameters[i].ParameterType;
            var argument = arguments[i];

            if (argument is null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) is null) throw new NullReferenceException();
            }
            else if (!type.IsInstanceOfType(argument))
            {
                throw new InvalidCastException(
                    $"Unable to cast object of type '{argument.GetType()}' to type '{type}'.");
            }
        }

        // DoNotWrapExceptions: an exception thrown by the constructor reaches the caller as it was thrown.
        return Constructor.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, arguments, culture: null);
    }

    // Compiling happens away from the thread that is resolving, so no request waits for it. If it fails, the
    // constructor simply stays on the reflection path, which reports whatever is wrong with it when it is used.
    private static void CompileInBackground<T>(Tier<T> tier, Func<T> compile) where T : class
    {
        ThreadPool.UnsafeQueueUserWorkItem(
            static work =>
            {
                try
                {
                    Volatile.Write(ref work.tier.Fast, work.compile());
                }
                catch (Exception)
                {
                }
            },
            (tier, compile),
            preferLocal: false);
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
        var array = Expression.Parameter(typeof(object[]), "args");

        var arguments = Parameters.Select((p, i) =>
            (Expression)Expression.Convert(Expression.ArrayIndex(array, Expression.Constant(i)), p.ParameterType));

        var body = Expression.Convert(Expression.New(Constructor, arguments), typeof(object));
        return Expression.Lambda<Func<object?[], object>>(body, array).Compile();
    }

    private Func<ResolveContext, object> CompileDirect()
    {
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
