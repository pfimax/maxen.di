namespace Maxen.DI;

/// <summary>A registration doesn't make sense, for example an implementation that doesn't match its service.</summary>
public class RegistrationException : DIException
{
    /// <summary>Creates the exception with a message.</summary>
    public RegistrationException(string message) : base(message) { }
}
