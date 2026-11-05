namespace Maxen.DI;

/// <summary>Services depend on each other in a loop (A needs B, B needs A).</summary>
public sealed class CircularDependencyException : ResolutionException
{
    /// <summary>Creates the exception with the dependency path that formed the loop.</summary>
    public CircularDependencyException(string path)
        : base($"Circular dependency detected: {path}") { }
}
