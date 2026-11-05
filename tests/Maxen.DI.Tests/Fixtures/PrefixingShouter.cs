namespace Maxen.DI.Tests.Fixtures;

/// <summary>Decorator with a higher priority number, so it wraps the exclaiming one from the outside.</summary>
[Decorator(typeof(IShouter), Priority = 5)]
public sealed class PrefixingShouter : IShouter
{
    private readonly IShouter _inner;

    public PrefixingShouter(IShouter inner) => _inner = inner;

    public string Shout(string text) => ">> " + _inner.Shout(text);
}
