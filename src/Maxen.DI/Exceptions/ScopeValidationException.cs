namespace Maxen.DI;

/// <summary>A lifetime rule was broken, such as a singleton holding a scoped service.</summary>
public sealed class ScopeValidationException : ResolutionException
{
    /// <summary>Creates the exception with a message describing the lifetime mistake.</summary>
    public ScopeValidationException(string message) : base(message) { }
}
