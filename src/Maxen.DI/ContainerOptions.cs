using Maxen.DI.Diagnostics;

namespace Maxen.DI;

/// <summary>Switches that change how strict the container is.</summary>
public sealed class ContainerOptions
{
    /// <summary>
    /// On by default. Stops you from resolving scoped services out of the root container and from
    /// letting a long-lived service (a singleton) hold on to a short-lived one (a scoped service).
    /// Both mistakes tend to show up as hard-to-trace bugs in production, so we fail early instead.
    /// </summary>
    public bool ValidateScopes { get; set; } = true;

    /// <summary>
    /// Off by default. When on, <c>Build()</c> checks the whole configuration up front and throws a
    /// <see cref="ContainerValidationException"/> listing every problem, so mistakes surface at startup
    /// instead of the first time a service happens to be resolved.
    /// </summary>
    public bool ValidateOnBuild { get; set; }

    /// <summary>
    /// Off by default. Validation reads constructors only, so services built by your own factory delegates
    /// are opaque to it. Turn this on and validation also <i>runs</i> each factory once, from a throwaway scope,
    /// and reports the ones that throw (usually a dependency the factory asks for that isn't registered).
    /// Factories run for real, so side effects happen and singletons built this way stay cached.
    /// </summary>
    public bool ValidateFactories { get; set; }

    /// <summary>
    /// On by default. Lets the container resolve plain registrations (types and factories without middleware,
    /// decorators or conditions) from a precomputed recipe, which is faster. Results are identical; turn it off only to
    /// compare behaviour or to rule it out while chasing a problem.
    /// </summary>
    public bool UseResolvePlans { get; set; } = true;

    /// <summary>
    /// Off by default, which keeps the rule "each owner disposes its own scopes". When on, disposing a scope (or the
    /// container) first disposes every scope that was created from it and is still alive, newest first.
    /// </summary>
    public bool DisposeChildScopes { get; set; }
}
