namespace Maxen.DI.Diagnostics;

/// <summary>The kind of problem container validation found.</summary>
public enum ValidationIssueKind
{
    /// <summary>A constructor needs something that isn't registered, or no constructor can be chosen.</summary>
    MissingDependency,

    /// <summary>Services depend on each other in a loop.</summary>
    CircularDependency,

    /// <summary>A longer-lived service depends on a shorter-lived one (a captive dependency).</summary>
    LifetimeMismatch,

    /// <summary>A factory registration threw when validation ran it (needs <c>ValidateFactories</c>).</summary>
    FactoryFailed,

    /// <summary>A <c>WhenInjectedInto</c> registration names a consumer that nothing registered can ever be.</summary>
    UnreachableRegistration,
}
