namespace Maxen.DI.Tests.Fixtures;

/// <summary>A service of its own, so the decorator attributes in these fixtures never touch other tests.</summary>
public interface IShouter
{
    string Shout(string text);
}
