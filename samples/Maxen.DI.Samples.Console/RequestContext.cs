namespace Maxen.DI.Samples.Console;

/// <summary>Shared by everything inside one "request" scope, however deeply nested.</summary>
public sealed class RequestContext
{
    public Guid Id { get; } = Guid.NewGuid();
}
