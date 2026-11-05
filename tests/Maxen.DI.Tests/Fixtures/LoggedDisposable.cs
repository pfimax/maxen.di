namespace Maxen.DI.Tests.Fixtures;

/// <summary>Writes its name into a shared log when disposed, so tests can check the order things were cleaned up.</summary>
public sealed class LoggedDisposable : IDisposable
{
    private readonly List<string> _log;
    private readonly string _name;

    public LoggedDisposable(List<string> log, string name)
    {
        _log = log;
        _name = name;
    }

    public void Dispose() => _log.Add(_name);
}
