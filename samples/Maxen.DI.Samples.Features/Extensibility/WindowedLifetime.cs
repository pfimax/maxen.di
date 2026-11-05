using Maxen.DI.Pipeline;

namespace Maxen.DI.Samples.Features.Extensibility;

/// <summary>A lifetime of our own: the instance is reused for a short window and then rebuilt.</summary>
internal sealed class WindowedLifetime : Lifetime
{
    private readonly TimeSpan _window;
    private readonly object _gate = new();
    private object? _instance;
    private DateTime _builtAt;

    public WindowedLifetime(TimeSpan window) => _window = window;

    public override string Name => "Windowed";

    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        lock (_gate)
        {
            if (_instance is null || DateTime.UtcNow - _builtAt > _window)
            {
                _instance = next(context);
                _builtAt = DateTime.UtcNow;
            }

            return _instance;
        }
    }
}
