namespace Maxen.DI;

/// <summary>Nothing is registered for the requested service.</summary>
public sealed class ServiceNotRegisteredException : ResolutionException
{
    /// <summary>Creates the exception for a missing service, with the path that led to it and any "did you mean" hints.</summary>
    public ServiceNotRegisteredException(ServiceKey key, string? path = null, IReadOnlyList<string>? suggestions = null)
        : base(BuildMessage(key, path, suggestions))
    {
        Key = key;
        Suggestions = suggestions ?? Array.Empty<string>();
    }

    /// <summary>The service that could not be found.</summary>
    public ServiceKey Key { get; }

    /// <summary>Things that are registered and look like what was asked for. Often empty.</summary>
    public IReadOnlyList<string> Suggestions { get; }

    private static string BuildMessage(ServiceKey key, string? path, IReadOnlyList<string>? suggestions)
    {
        var message = $"No registration found for '{key}'.";
        if (path is not null) message += $" Resolution path: {path}";
        if (suggestions is { Count: > 0 }) message += $" Did you mean: {string.Join("; ", suggestions)}?";
        return message;
    }
}
