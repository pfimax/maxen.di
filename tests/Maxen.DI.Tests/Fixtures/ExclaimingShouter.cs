namespace Maxen.DI.Tests.Fixtures;

/// <summary>Decorator that sits closest to the real shouter (priority 0) and adds an exclamation mark.</summary>
[Decorator(typeof(IShouter))]
public sealed class ExclaimingShouter : IShouter
{
    private readonly IShouter _inner;

    public ExclaimingShouter(IShouter inner) => _inner = inner;

    public string Shout(string text) => _inner.Shout(text) + "!";
}
