namespace Maxen.DI;

/// <summary>Base type for every error Maxen.DI raises, so callers can catch them all in one place.</summary>
public class DIException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public DIException(string message) : base(message) { }
    /// <summary>Creates the exception with a message and the error that caused it.</summary>
    public DIException(string message, Exception inner) : base(message, inner) { }
}
