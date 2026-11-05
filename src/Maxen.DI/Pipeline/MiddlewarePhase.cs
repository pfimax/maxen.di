namespace Maxen.DI.Pipeline;

/// <summary>Where in the pipeline a piece of middleware runs.</summary>
public enum MiddlewarePhase
{
    /// <summary>On every resolve, before the lifetime decides whether to reuse a cached instance. Good for logging or auditing.</summary>
    PreLifetime,

    /// <summary>Only when a new instance is really being built, wrapped around construction. Good for post-processing.</summary>
    PostLifetime,
}
