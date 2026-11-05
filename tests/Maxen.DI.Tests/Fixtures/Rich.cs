namespace Maxen.DI.Tests.Fixtures;

/// <summary>The real service behind <see cref="IRich"/>.</summary>
public sealed class Rich : IRich
{
    public int Count { get; private set; }

    public string Name { get; set; } = "start";

    public int Add(int a, int b)
    {
        Count++;
        return a + b;
    }

    public void Reset() => Count = 0;

    public Task<string> LoadAsync(string @event) => Task.FromResult("loaded " + @event);
}
