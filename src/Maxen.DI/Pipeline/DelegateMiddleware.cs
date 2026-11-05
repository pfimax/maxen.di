namespace Maxen.DI.Pipeline;

/// <summary>Lets you write middleware as a lambda instead of a class.</summary>
public sealed class DelegateMiddleware : IResolutionMiddleware
{
    private readonly Func<ResolveContext, ResolveDelegate, object> _func;

    /// <summary>Wraps a delegate so it can be used as middleware.</summary>
    public DelegateMiddleware(Func<ResolveContext, ResolveDelegate, object> func) => _func = func;

    /// <inheritdoc/>
    public object Invoke(ResolveContext context, ResolveDelegate next) => _func(context, next);
}
