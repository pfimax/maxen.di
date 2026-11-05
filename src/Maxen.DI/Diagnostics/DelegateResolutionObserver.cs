namespace Maxen.DI.Diagnostics;

/// <summary>Lets an observer be written as a lambda.</summary>
public sealed class DelegateResolutionObserver : IResolutionObserver
{
    private readonly Action<ResolutionEvent> _action;

    /// <summary>Wraps a delegate so it can be used as an observer.</summary>
    public DelegateResolutionObserver(Action<ResolutionEvent> action) => _action = action;

    /// <inheritdoc/>
    public void OnResolved(ResolutionEvent resolution) => _action(resolution);
}
