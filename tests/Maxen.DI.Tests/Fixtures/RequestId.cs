namespace Maxen.DI.Tests.Fixtures;

/// <summary>A scoped service whose identity shows whether two things shared a request scope.</summary>
public sealed class RequestId
{
    public Guid Value { get; } = Guid.NewGuid();
}
