namespace Maxen.DI;

/// <summary>Something went wrong while building a service.</summary>
public class ResolutionException : DIException
{
    /// <summary>Creates the exception with a message.</summary>
    public ResolutionException(string message) : base(message) { }
    /// <summary>Creates the exception with a message and the error that caused it.</summary>
    public ResolutionException(string message, Exception inner) : base(message, inner) { }
}
