namespace Maxen.DI.Tests.Fixtures;

/// <summary>Depends on <see cref="Disposable"/>; registered as a singleton it captures a scoped service.</summary>
public class NeedsDisposable
{
    public NeedsDisposable(Disposable disposable) { }
}
