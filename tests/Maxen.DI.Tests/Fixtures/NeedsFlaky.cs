namespace Maxen.DI.Tests.Fixtures;

/// <summary>A service whose dependency comes from a source that fails the first time it is asked.</summary>
public class NeedsFlaky
{
    public NeedsFlaky(FlakyDependency dependency) => Dependency = dependency;

    public FlakyDependency Dependency { get; }
}
