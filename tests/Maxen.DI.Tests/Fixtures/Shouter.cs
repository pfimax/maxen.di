namespace Maxen.DI.Tests.Fixtures;

/// <summary>The undecorated service; returns the text as it came.</summary>
[Service(ServiceType = typeof(IShouter))]
public sealed class Shouter : IShouter
{
    public string Shout(string text) => text;
}
