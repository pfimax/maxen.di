namespace Maxen.DI.Tests.Fixtures;

/// <summary>An interface with most of the shapes a proxy has to forward: properties, void, async, keyword-named parameters, inheritance.</summary>
public interface IRich : ICounted
{
    string Name { get; set; }

    int Add(int a, int b);

    void Reset();

    Task<string> LoadAsync(string @event);
}
